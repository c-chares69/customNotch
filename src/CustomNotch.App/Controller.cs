using System.Windows;
using System.Windows.Threading;
using CustomNotch.App.Notch;
using CustomNotch.Core;
using CustomNotch.Core.Actions;
using CustomNotch.Core.Config;
using CustomNotch.Core.Model;
using CustomNotch.Core.Platform;
using CustomNotch.Core.Sources;

namespace CustomNotch.App;

/// <summary>Assemblage et cycle de vie : config → ordonnanceur → pilules, tray, tic d'une seconde. Tout ce qui vient
/// d'un autre thread (lectures, rechargement de config) repasse par le Dispatcher ici et nulle part ailleurs.</summary>
public sealed class Controller : IPillHost
{
    private readonly string _home;
    private readonly Platform.WindowsMediaSession _media = Platform.WindowsMediaSession.Create();
    private readonly SourceRegistry _registry;
    private readonly ReadingStore _readings = new();
    private readonly ConfigStore _config;
    private readonly ConfigEditor _editor;
    private readonly Scheduler _scheduler;
    private readonly Dictionary<string, PillWindow> _pills = new();
    private readonly TrayIcon _tray = new();
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(1) };
    private Settings.SettingsWindow? _settings;
    private bool _allHidden;
    private bool _stopped;

    public Controller(string home)
    {
        _home = home;
        _registry = CoreSources.Build(_media);
        _config = new ConfigStore(home, _registry.Schemas);
        _editor = new ConfigEditor(_config, _registry.Schemas);
        _scheduler = new Scheduler(_registry, _readings, () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Core.Platform.Idle.Ms);
    }

    private static Dispatcher Ui => Application.Current.Dispatcher;

    public void Start()
    {
        Theme.Apply(Application.Current, _config.App);
        // Windows qui bascule clair/sombre pendant que l'app tourne : les pinceaux sont reposés, les menus suivent
        // (la pilule, elle, reste noire : c'est son identité). L'événement arrive sur un thread système.
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        _config.Changed += file => Ui.BeginInvoke(() => ApplyConfig(file));
        _config.Rejected += message => Ui.BeginInvoke(() => _tray.Notify("Configuration refusée", message));
        _readings.Changed += id => Ui.BeginInvoke(() => OnReadingChanged(id));
        _tray.RefreshRequested += RefreshAll;
        _tray.SettingsRequested += ShowSettings;
        _tray.QuitRequested += Quit;
        _tray.ToggleAllRequested += ToggleAll;
        _tray.PillToggleRequested += TogglePill;
        _tick.Tick += (_, _) =>
        {
            foreach (var p in _pills.Values)
            {
                p.SetFullScreen(FullScreenDetector.IsFullScreenOn(p.CurrentScreen));
                p.Tick();
            }
        };
        _tick.Start();
        if (!_config.Load()) _tray.Notify("Configuration refusée", string.Join(" ; ", _config.LastErrors));
        _config.StartWatching();
        Log.Info("app", $"{Core.App.Name} {Core.App.Version} démarré, cells.json : {_config.CellsPath}");
    }

    private void ApplyConfig(CellsFile file)
    {
        _scheduler.Apply(file);
        var wanted = file.Pills.ToDictionary(p => p.Id);
        foreach (var id in _pills.Keys.Where(id => !wanted.ContainsKey(id)).ToList())
        {
            _pills[id].Close();
            _pills.Remove(id);
        }
        foreach (var pill in file.Pills)
        {
            if (_pills.TryGetValue(pill.Id, out var window)) window.Apply(pill);
            else
            {
                window = new PillWindow(pill, this);
                _pills[pill.Id] = window;
            }
            window.SetWanted(pill.Visible && !_allHidden);
        }
        _tray.SetPills(file.Pills.Select(p => (p.Id, p.Visible)));
    }

    private void OnReadingChanged(string cellId)
    {
        foreach (var pill in _pills.Values) pill.UpdateCell(cellId);
    }

    public void RefreshAll()
    {
        foreach (var cell in _config.Current.AllCells()) _scheduler.RefreshNow(cell.Id);
    }

    public void ToggleAll()
    {
        _allHidden = !_allHidden;
        foreach (var (id, window) in _pills)
        {
            var visible = _config.Current.Pills.First(p => p.Id == id).Visible;
            window.SetWanted(visible && !_allHidden);
        }
    }

    public void TogglePill(string id)
    {
        var current = _config.Current.Pills.FirstOrDefault(p => p.Id == id)?.Visible ?? true;
        _config.SetPillLocal(id, p => p["visible"] = !current);
    }

    /// <summary>Une seule fenêtre Réglages : réouverte ou ramenée au premier plan.</summary>
    public void ShowSettings()
    {
        if (_settings is null)
        {
            _settings = new Settings.SettingsWindow(new Settings.SettingsContext(_config, _editor, _registry));
            _settings.Closed += (_, _) => _settings = null;
        }
        _settings.Show();
        if (_settings.WindowState == WindowState.Minimized) _settings.WindowState = WindowState.Normal;
        _settings.Activate();
    }

    public void Quit()
    {
        // Le watchdog (tâche planifiée, --auto) relance l'application quand elle est tombée ; pas quand on l'a
        // quittée soi-même. Le marqueur est levé au prochain lancement volontaire.
        SingleInstance.MarkStoppedByUser(_home);
        Stop();
        Application.Current.Shutdown();
    }

    /// <summary>Idempotente : Quit() et App.OnExit l'appellent tous les deux, le second appel ne doit rien refaire.</summary>
    private void OnUserPreferenceChanged(object? sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if (e.Category != Microsoft.Win32.UserPreferenceCategory.General) return;
        Ui.BeginInvoke(() => { if (!_stopped) Theme.Apply(Application.Current, _config.App); });
    }

    public void Stop()
    {
        if (_stopped) return;
        _stopped = true;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _tick.Stop();
        _scheduler.Dispose();
        _config.Dispose();
        _media.Dispose();
        _tray.Dispose();
        _settings?.Close();
        foreach (var p in _pills.Values) p.Close();
        _pills.Clear();
    }

    // ---- IPillHost -------------------------------------------------------------------------------------------

    public CellView? View(string cellId)
    {
        var cell = _config.Current.Cell(cellId);
        if (cell is null) return null;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (cell.IsGroup) return CellViews.FromGroup(cell, Children(cell), now);
        var reading = _readings.Get(cellId) ?? Reading.Empty;
        return CellViews.From(cell, reading, now);
    }

    public IReadOnlyList<CellView> Children(CellConfig group)
        => (group.Children ?? new List<string>()).Select(View).Where(v => v is not null).Select(v => v!).ToList();

    public Task RunActionAsync(string cellId, ActionConfig action) => ActionRunner.RunAsync(action, a => _scheduler.InvokeAsync(cellId, a));
    public Task InvokeSourceAsync(string cellId, string action) => _scheduler.InvokeAsync(cellId, action);

    public void SavePosition(string pillId, double along, string? screen)
        => _config.SetPillLocal(pillId, p => { p["along"] = Math.Round(along, 4); p["screen"] = screen; });

    public void RequestRefresh(string cellId) => _scheduler.RefreshNow(cellId);
    public void HidePill(string pillId) => _config.SetPillLocal(pillId, p => p["visible"] = false);
    public SourceSchema? Schema(string sourceType) => _registry.Get(sourceType)?.Schema;
}

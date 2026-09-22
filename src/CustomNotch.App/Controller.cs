using System.Windows;
using System.Windows.Threading;
using CustomNotch.App.Notch;
using CustomNotch.Core;
using CustomNotch.Core.Actions;
using CustomNotch.Core.Config;
using CustomNotch.Core.Model;
using CustomNotch.Core.Sources;

namespace CustomNotch.App;

/// <summary>Assemblage et cycle de vie : config → ordonnanceur → pilules, tray, tic d'une seconde. Tout ce qui vient
/// d'un autre thread (lectures, rechargement de config) repasse par le Dispatcher ici et nulle part ailleurs.</summary>
public sealed class Controller : IPillHost
{
    private readonly string _home;
    private readonly SourceRegistry _registry = CoreSources.Build();
    private readonly ReadingStore _readings = new();
    private readonly ConfigStore _config;
    private readonly Scheduler _scheduler;
    private readonly Dictionary<string, PillWindow> _pills = new();
    private readonly TrayIcon _tray = new();
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _allHidden;
    private bool _stopped;

    public Controller(string home)
    {
        _home = home;
        _config = new ConfigStore(home, _registry.Types);
        _scheduler = new Scheduler(_registry, _readings, () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Core.Platform.Idle.Ms);
    }

    private static Dispatcher Ui => Application.Current.Dispatcher;

    public void Start()
    {
        Theme.Apply(Application.Current, _config.App);
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
            foreach (var (id, p) in _pills)
            {
                var visible = _config.Current.Pills.FirstOrDefault(x => x.Id == id)?.Visible ?? false;
                if (visible && !_allHidden) p.SetFullScreen(FullScreenDetector.IsFullScreenOn(p.CurrentScreen));
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
            if (pill.Visible && !_allHidden) window.Show(); else window.Hide();
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
            if (visible && !_allHidden) window.Show(); else window.Hide();
        }
    }

    public void TogglePill(string id)
    {
        var current = _config.Current.Pills.FirstOrDefault(p => p.Id == id)?.Visible ?? true;
        _config.SetPillLocal(id, p => p["visible"] = !current);
    }

    /// <summary>Pas encore de fenêtre de réglages (plan 2) : on ouvre cells.json dans l'éditeur par défaut.</summary>
    public void ShowSettings() => ActionRunner.Open(_config.CellsPath);

    public void Quit()
    {
        Stop();
        Application.Current.Shutdown();
    }

    /// <summary>Idempotente : Quit() et App.OnExit l'appellent tous les deux, le second appel ne doit rien refaire.</summary>
    public void Stop()
    {
        if (_stopped) return;
        _stopped = true;
        _tick.Stop();
        _scheduler.Dispose();
        _config.Dispose();
        _tray.Dispose();
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
}

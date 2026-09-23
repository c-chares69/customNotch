using System.IO;
using System.Windows;
using System.Windows.Controls;
using CustomNotch.Core;
using CustomNotch.Core.Actions;
using CustomNotch.Core.Platform;

namespace CustomNotch.App.Settings;

public sealed class GeneralPage : PageBase
{
    private static readonly (string, string)[] Themes = { ("system", "Automatique (suit Windows)"), ("light", "Clair"), ("dark", "Sombre") };
    private static readonly (string, string)[] Activities = { ("dot", "Pastille seule"), ("ring", "Anneau animé") };
    private readonly TextBox _cellsPath;
    private readonly CheckBox _autostart = new() { Content = "Lancer customNotch à l'ouverture de session" };
    private readonly TextBlock _autostartError = Ui.Text("Impossible de modifier le démarrage automatique.", 11, null, "Muted");

    public GeneralPage(SettingsContext ctx) : base(ctx, "Général", "L'emplacement de la configuration, le démarrage, le thème.")
    {
        _cellsPath = new TextBox { Text = ctx.Store.CellsPath, IsReadOnly = true, Width = 460, HorizontalAlignment = HorizontalAlignment.Left };
        var browse = Btn("Parcourir…", () =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "cells.json|*.json", FileName = ctx.Store.CellsPath, CheckFileExists = false };
            if (dialog.ShowDialog() != true) return;
            ctx.Store.App.Set("cells_path", dialog.FileName);
            ctx.Store.App.Save();
            _cellsPath.Text = dialog.FileName + "  (au prochain démarrage)";
        });
        var openDir = Btn("Ouvrir le dossier", () => ActionRunner.Open(Path.GetDirectoryName(ctx.Store.CellsPath)!));
        var pathRow = new StackPanel { Orientation = Orientation.Horizontal };
        pathRow.Children.Add(_cellsPath); browse.Margin = new Thickness(8, 0, 8, 0); pathRow.Children.Add(browse); pathRow.Children.Add(openDir);
        var hint = Ui.Text("Mets ce fichier dans un dossier synchronisé pour retrouver tes pilules sur une autre machine ; la position et l'écran restent propres à chaque poste.", 11, null, "Muted");
        hint.TextWrapping = TextWrapping.Wrap;

        Body.Children.Add(Banner);
        Body.Children.Add(Section("Configuration"));
        Body.Children.Add(Card(Stack(Row("Fichier cells.json", pathRow), hint)));

        _autostartError.Visibility = Visibility.Collapsed;
        _autostart.IsChecked = Autostart.IsEnabled();
        // SetEnabled peut échouer (script introuvable, tâche verrouillée…) : la case reflète alors l'état réel,
        // pas le clic — remise en place sans redéclencher Checked/Unchecked (sinon boucle infinie).
        var revertingAutostart = false;
        _autostart.Checked += (_, _) => OnAutostartToggled(true);
        _autostart.Unchecked += (_, _) => OnAutostartToggled(false);
        // schtasks / le script de l'installateur peuvent prendre quelques secondes : la case se grise le temps de
        // l'opération plutôt que de figer la fenêtre, et reflète l'état réel à la fin.
        void OnAutostartToggled(bool enabled)
        {
            if (revertingAutostart) return;
            _autostart.IsEnabled = false;
            _ = Task.Run(() => Autostart.SetEnabled(enabled)).ContinueWith(t => Dispatcher.BeginInvoke(() =>
            {
                var ok = t.Status == TaskStatus.RanToCompletion && t.Result;
                if (!ok)
                {
                    revertingAutostart = true;
                    _autostart.IsChecked = Autostart.IsEnabled();
                    revertingAutostart = false;
                }
                _autostartError.Visibility = ok ? Visibility.Collapsed : Visibility.Visible;
                _autostart.IsEnabled = true;
            }));
        }
        var theme = Combo(Themes, ctx.Store.App.GetString("appearance.theme", "system"), v =>
        {
            ctx.Store.App.Set("appearance.theme", v);
            ctx.Store.App.Save();
            Theme.Apply(Application.Current, ctx.Store.App);
        });

        var activity = Combo(Activities, ctx.Store.Current.Appearance.Activity, v => Try(() => ctx.Editor.SetAppearance(a => a["activity"] = v)));
        var cardScale = Bricks.Slider(ctx.Store.Current.Appearance.CardScale, 1.0, 1.5, 0.05,
            v => Try(() => ctx.Editor.SetAppearance(a => a["cardScale"] = Math.Round(v, 2))), v => $"{Math.Round(v * 100)} %", "cardScale");
        Body.Children.Add(Section("Apparence"));
        Body.Children.Add(Card(Stack(Row("Indicateur d'activité", activity), Row("Échelle de la carte", cardScale))));

        Body.Children.Add(Section("Mises à jour"));
        Body.Children.Add(Card(BuildUpdatesCard(ctx)));

        Body.Children.Add(Section("Journal et diagnostic"));
        Body.Children.Add(Card(BuildDiagnosticsCard()));

        Body.Children.Add(Section("Poste"));
        Body.Children.Add(Card(Stack(Row("Démarrage", _autostart), _autostartError, Row("Thème des fenêtres", theme))));

        var repo = Btn("Le dépôt sur GitHub", () => ActionRunner.Open("https://github.com/c-chares69/customNotch"), "GhostButton");
        var about = new StackPanel { Orientation = Orientation.Horizontal };
        about.Children.Add(repo);
        Body.Children.Add(Section("À propos"));
        Body.Children.Add(Card(Stack(Row($"customNotch {Core.App.Version}", about))));
    }

    /// <summary>Vérification automatique, adresse du manifeste, cadence, et les trois actions (vérifier, installer,
    /// ignorer) ; la ligne de statut suit les événements d'<see cref="UpdateChecker"/> et se désabonne dans
    /// <see cref="Detach"/> (via <c>OnDetach</c>, PageBase) — la fenêtre Réglages construit chaque page une seule
    /// fois et la referme plutôt que de la reconstruire.</summary>
    private UIElement BuildUpdatesCard(SettingsContext ctx)
    {
        var updates = ctx.Updates;
        var autoCheck = new CheckBox { Content = "Vérifier automatiquement", IsChecked = ctx.Store.App.GetBool("updates.enabled", true) };
        autoCheck.Checked += (_, _) => { ctx.Store.App.Set("updates.enabled", true); ctx.Store.App.Save(); };
        autoCheck.Unchecked += (_, _) => { ctx.Store.App.Set("updates.enabled", false); ctx.Store.App.Save(); };

        var urlHint = Ui.Text($"Adresse par défaut : {Updates.DefaultManifestUrl}", 11, null, "Muted");
        urlHint.TextWrapping = TextWrapping.Wrap;
        var urlField = Debounced(ctx.Store.App.GetString("updates.url"), v =>
        {
            ctx.Store.App.Set("updates.url", v.Trim());
            ctx.Store.App.Save();
        }, 460);
        void RefreshUrlHint() => urlHint.Visibility = urlField.Text.Trim().Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        RefreshUrlHint();
        urlField.TextChanged += (_, _) => RefreshUrlHint();

        var interval = Bricks.Slider(ctx.Store.App.GetDouble("updates.interval_hours", 24), 1, 720, 1,
            v => { ctx.Store.App.Set("updates.interval_hours", Math.Round(v)); ctx.Store.App.Save(); }, v => $"{Math.Round(v)} h");

        var status = Ui.Text(updates.Latest is { } pending ? $"{pending.Version} disponible." : "", 11, null, "Muted");
        status.TextWrapping = TextWrapping.Wrap;
        var install = Btn("Installer", () => { if (updates.Latest is { } r) updates.Prepare(r); });
        Button skip = null!;
        skip = Btn("Ignorer cette version", () =>
        {
            if (updates.Latest is not { } r) return;
            updates.Skip(r.Version);
            status.Text = $"Version {r.Version} ignorée.";
            install.Visibility = Visibility.Collapsed;
            skip.Visibility = Visibility.Collapsed;
        });
        install.Visibility = skip.Visibility = updates.Latest is null ? Visibility.Collapsed : Visibility.Visible;
        var check = Btn("Vérifier maintenant", () => { status.Text = "Vérification…"; updates.Check(manual: true); }, "Primary");

        void OnUpToDate(string version) => Dispatcher.BeginInvoke(() =>
        {
            status.Text = $"À jour ({version}).";
            install.Visibility = skip.Visibility = Visibility.Collapsed;
        });
        void OnAvailable(Release release) => Dispatcher.BeginInvoke(() =>
        {
            status.Text = $"{release.Version} disponible.{(release.Notes.Length > 0 ? " " + release.Notes : "")}";
            install.Visibility = skip.Visibility = Visibility.Visible;
        });
        void OnFailed(string message, bool manual) => Dispatcher.BeginInvoke(() => status.Text = message);
        void OnProgress(string progress) => Dispatcher.BeginInvoke(() => status.Text = progress);
        updates.UpToDate += OnUpToDate;
        updates.Available += OnAvailable;
        updates.Failed += OnFailed;
        updates.Progress += OnProgress;
        OnDetach(() =>
        {
            updates.UpToDate -= OnUpToDate;
            updates.Available -= OnAvailable;
            updates.Failed -= OnFailed;
            updates.Progress -= OnProgress;
        });

        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        actions.Children.Add(check); actions.Children.Add(install); actions.Children.Add(skip);
        return Stack(Row("Vérification", autoCheck), Row("Adresse du manifeste", urlField), urlHint, Row("Toutes les", interval), actions, status);
    }

    /// <summary>Le journal existant, et « Signaler un problème » qui dépose une archive de diagnostic (hors thread
    /// UI : <see cref="Diagnostics.MakeReport"/> lit et zippe plusieurs fichiers).</summary>
    private UIElement BuildDiagnosticsCard()
    {
        var status = Ui.Text("", 11, null, "Muted");
        status.TextWrapping = TextWrapping.Wrap;
        var journal = Btn("Ouvrir le journal", () => { if (Log.Directory is { } d) ActionRunner.Open(d); });
        var report = Btn("Signaler un problème…", () =>
        {
            status.Text = "Rapport en cours…";
            Task.Run(() =>
            {
                try { return Diagnostics.MakeReport(); }
                catch (IOException) { return null; }
            }).ContinueWith(t => Dispatcher.BeginInvoke(() =>
                status.Text = t.Result is { } path
                    ? $"Rapport déposé sur le Bureau : {Path.GetFileName(path)}"
                    : "Rapport impossible à écrire."));
        });
        var hint = Ui.Text("Erreurs de démarrage, écritures en attente : tout y passe. « Signaler un problème » dépose sur le Bureau une archive de diagnostic : journaux, configuration sans aucun secret, informations système - à joindre à ta demande d'aide.", 11, null, "Muted");
        hint.TextWrapping = TextWrapping.Wrap;
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        actions.Children.Add(journal); actions.Children.Add(report);
        return Stack(actions, status, hint);
    }

    private static StackPanel Stack(params UIElement[] children)
    {
        var s = new StackPanel();
        foreach (var c in children) s.Children.Add(c);
        return s;
    }
}

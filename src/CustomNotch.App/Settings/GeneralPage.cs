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
    private readonly TextBlock _autostartError;

    public GeneralPage(SettingsContext ctx) : base(ctx, "Général", "Ce poste, l'apparence, les mises à jour.")
    {
        _cellsPath = new TextBox { Text = ctx.Store.CellsPath, IsReadOnly = true, Width = 460, HorizontalAlignment = HorizontalAlignment.Left };
        var browse = Bricks.Btn(this, "Parcourir…", () =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "cells.json|*.json", FileName = ctx.Store.CellsPath, CheckFileExists = false };
            if (dialog.ShowDialog() != true) return;
            ctx.Store.App.Set("cells_path", dialog.FileName);
            ctx.Store.App.Save();
            _cellsPath.Text = dialog.FileName + "  (au prochain démarrage)";
        });
        var openDir = Bricks.Btn(this, "Ouvrir le dossier", () => ActionRunner.Open(Path.GetDirectoryName(ctx.Store.CellsPath)!));
        var pathRow = new StackPanel { Orientation = Orientation.Horizontal };
        pathRow.Children.Add(_cellsPath); browse.Margin = new Thickness(8, 0, 8, 0); pathRow.Children.Add(browse); pathRow.Children.Add(openDir);
        var cellsHint = Bricks.Hint(this, "Mets ce fichier dans un dossier synchronisé pour retrouver tes pilules sur une autre machine ; la position et l'écran restent propres à chaque poste.");

        Body.Children.Add(Banner);
        Body.Children.Add(Bricks.Card(this, "Configuration", Bricks.Row("Fichier cells.json", pathRow), cellsHint));

        // SetEnabled peut échouer (script introuvable, tâche verrouillée…) : la case reflète alors l'état réel, pas
        // le clic. Bricks.Check s'accroche à Click (jamais déclenché par un changement d'IsChecked posé en code),
        // donc reposer _autostart.IsChecked ici après un échec ne rappelle pas Commit en boucle.
        _autostartError = Bricks.Hint(this, "");
        _autostartError.Visibility = Visibility.Collapsed;
        CheckBox autostart = null!;
        void OnAutostartToggled(bool enabled)
        {
            autostart.IsEnabled = false;
            _ = Task.Run(() => Autostart.SetEnabled(enabled)).ContinueWith(t => Dispatcher.BeginInvoke(() =>
            {
                var ok = t.Status == TaskStatus.RanToCompletion && t.Result;
                if (!ok) autostart.IsChecked = Autostart.IsEnabled();
                _autostartError.Text = "Impossible de modifier le démarrage automatique.";
                _autostartError.Visibility = ok ? Visibility.Collapsed : Visibility.Visible;
                autostart.IsEnabled = true;
            }));
        }
        autostart = Bricks.Check(this, "Lancer customNotch à l'ouverture de session", Autostart.IsEnabled(), OnAutostartToggled);

        var activity = Bricks.Combo(this, "Indicateur d'activité", Activities, ctx.Store.Current.Appearance.Activity, v => Try(() => ctx.Editor.SetAppearance(a => a["activity"] = v)));
        var cardScale = Bricks.Slider(ctx.Store.Current.Appearance.CardScale, 1.0, 1.5, 0.05,
            v => Try(() => ctx.Editor.SetAppearance(a => a["cardScale"] = Math.Round(v, 2))), v => $"{Math.Round(v * 100)} %", "cardScale");
        var theme = Bricks.Combo(this, "Thème des fenêtres", Themes, ctx.Store.App.GetString("appearance.theme", "system"), v =>
        {
            ctx.Store.App.Set("appearance.theme", v);
            ctx.Store.App.Save();
            Theme.Apply(Application.Current, ctx.Store.App);
        });
        Body.Children.Add(Bricks.Card(this, "Apparence", activity, Bricks.Row("Échelle de la carte", cardScale), theme));

        Body.Children.Add(BuildUpdatesCard(ctx));
        Body.Children.Add(BuildDiagnosticsCard());
        Body.Children.Add(Bricks.Card(this, "Poste", autostart, _autostartError));

        var about = Bricks.Card(this, "À propos",
            Bricks.Hint(this, $"customNotch {Core.App.Version}"),
            Bricks.Action(this, "Le dépôt sur GitHub", () => ActionRunner.Open("https://github.com/c-chares69/customNotch")));
        Body.Children.Add(about);
    }

    /// <summary>Vérification automatique, adresse du manifeste, cadence, et les trois actions (vérifier, installer,
    /// ignorer) ; la ligne de statut suit les événements d'<see cref="UpdateChecker"/> et se désabonne dans
    /// <see cref="Detach"/> (via <c>OnDetach</c>, PageBase) — la fenêtre Réglages construit chaque page une seule
    /// fois et la referme plutôt que de la reconstruire.</summary>
    private UIElement BuildUpdatesCard(SettingsContext ctx)
    {
        var updates = ctx.Updates;
        var autoCheck = Bricks.Check(this, "Vérifier automatiquement", ctx.Store.App.GetBool("updates.enabled", true), v =>
        {
            ctx.Store.App.Set("updates.enabled", v);
            ctx.Store.App.Save();
        });

        var url = Bricks.Text(this, "Adresse du manifeste", ctx.Store.App.GetString("updates.url"), v =>
        {
            ctx.Store.App.Set("updates.url", v);
            ctx.Store.App.Save();
        }, placeholder: $"Adresse par défaut : {Updates.DefaultManifestUrl}");

        var interval = Bricks.Number(this, "Toutes les", ctx.Store.App.GetDouble("updates.interval_hours", 24), 1, 720, v =>
        {
            ctx.Store.App.Set("updates.interval_hours", v);
            ctx.Store.App.Save();
        }, " h");

        var status = Bricks.Hint(this, updates.Latest is { } pending ? $"{pending.Version} disponible." : "");
        var install = Bricks.Action(this, "Installer", () => { if (updates.Latest is { } r) updates.Prepare(r); }, primary: true);
        Button skip = null!;
        skip = Bricks.Action(this, "Ignorer cette version", () =>
        {
            if (updates.Latest is not { } r) return;
            updates.Skip(r.Version);
            status.Text = $"Version {r.Version} ignorée.";
            install.Visibility = Visibility.Collapsed;
            skip.Visibility = Visibility.Collapsed;
        });
        install.Visibility = skip.Visibility = updates.Latest is null ? Visibility.Collapsed : Visibility.Visible;
        var check = Bricks.Action(this, "Vérifier maintenant", () => { status.Text = "Vérification…"; updates.Check(manual: true); });

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
        return Bricks.Card(this, "Mises à jour", autoCheck, url, interval, actions, status);
    }

    /// <summary>Le journal existant, et « Signaler un problème » qui dépose une archive de diagnostic (hors thread
    /// UI : <see cref="Diagnostics.MakeReport"/> lit et zippe plusieurs fichiers).</summary>
    private UIElement BuildDiagnosticsCard()
    {
        var status = Bricks.Hint(this, "");
        var journal = Bricks.Action(this, "Ouvrir le journal", () => { if (Log.Directory is { } d) ActionRunner.Open(d); });
        var report = Bricks.Action(this, "Signaler un problème…", () =>
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
        var hint = Bricks.Hint(this, "Erreurs de démarrage, écritures en attente : tout y passe. « Signaler un problème » dépose sur le Bureau une archive de diagnostic : journaux, configuration sans aucun secret, informations système - à joindre à ta demande d'aide.");
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        actions.Children.Add(journal); actions.Children.Add(report);
        return Bricks.Card(this, "Journal et diagnostic", actions, status, hint);
    }
}

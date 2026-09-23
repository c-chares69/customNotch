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

        Body.Children.Add(Section("Poste"));
        Body.Children.Add(Card(Stack(Row("Démarrage", _autostart), _autostartError, Row("Thème des fenêtres", theme))));

        var journal = Btn("Ouvrir le journal", () => { if (Log.Directory is { } d) ActionRunner.Open(d); });
        var repo = Btn("Le dépôt sur GitHub", () => ActionRunner.Open("https://github.com/c-chares69/customNotch"), "GhostButton");
        var about = new StackPanel { Orientation = Orientation.Horizontal };
        about.Children.Add(journal); about.Children.Add(repo);
        Body.Children.Add(Section("À propos"));
        Body.Children.Add(Card(Stack(Row($"customNotch {Core.App.Version}", about))));
    }

    private static StackPanel Stack(params UIElement[] children)
    {
        var s = new StackPanel();
        foreach (var c in children) s.Children.Add(c);
        return s;
    }
}

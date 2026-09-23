using System.Windows;
using System.Windows.Controls;
using CustomNotch.Core.Config;

namespace CustomNotch.App.Settings;

/// <summary>Les réglages globaux par type de source (aucun dans cette version : la carte le dit) et la liste des secrets —
/// leurs noms, jamais leurs valeurs.</summary>
public sealed class SourcesPage : PageBase
{
    private readonly EditorBanner _banner = new();
    private readonly StackPanel _secrets = new();

    public SourcesPage(SettingsContext ctx) : base(ctx, "Sources", "Réglages globaux par type de source et secrets.")
    {
        Body.Children.Add(_banner);
        Body.Children.Add(Bricks.Card(this, "Réglages globaux",
            Bricks.Hint(this, "Aucune source livrée avec cette version n'a de réglage global. Claude Code et ClickUp en auront.")));
        Body.Children.Add(Bricks.Card(this, "Secrets", _secrets,
            Bricks.Hint(this, "secrets.json, chiffré avec ton compte Windows (DPAPI).")));
        OnStoreChanged(Refresh);
    }

    public override void Refresh()
    {
        _secrets.Children.Clear();
        var names = Ctx.Store.Secrets.Names.OrderBy(n => n).ToList();
        if (names.Count == 0) { _secrets.Children.Add(Ui.Text("Aucun secret. Un champ « secret » d'une cellule en crée un.", 12, null, "Muted")); return; }
        foreach (var name in names)
        {
            var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
            // Retirer un secret qu'un champ requis résout encore est refusé (ConfigEditor restaure la valeur) :
            // le message remonte ici, sous la liste, plutôt que de disparaître dans une exception non attrapée.
            var remove = Bricks.Btn(this, "Effacer", () =>
            {
                try { Ctx.Editor.RemoveSecret(name); _banner.Clear(); }
                catch (ConfigException ex) { _banner.Show(ex.Message); }
            }, "GhostButton");
            DockPanel.SetDock(remove, Dock.Right);
            row.Children.Add(remove);
            var defined = Ui.Text("défini", 11, null, "Muted"); defined.Margin = new Thickness(12, 0, 12, 0); DockPanel.SetDock(defined, Dock.Right);
            row.Children.Add(defined);
            row.Children.Add(Ui.Text(name, 13));
            _secrets.Children.Add(row);
        }
    }
}

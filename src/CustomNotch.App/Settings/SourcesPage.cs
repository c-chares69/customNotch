using System.Windows;
using System.Windows.Controls;

namespace CustomNotch.App.Settings;

/// <summary>Les réglages globaux par type de source (aucun dans cette version : la page le dit) et la liste des secrets —
/// leurs noms, jamais leurs valeurs.</summary>
public sealed class SourcesPage : PageBase
{
    private readonly StackPanel _secrets = new();

    public SourcesPage(SettingsContext ctx) : base(ctx, "Sources", "Ce qui vaut pour toutes les cellules d'un même type, et les secrets chiffrés sur ce poste.")
    {
        var none = Ui.Text("Aucune source livrée avec cette version n'a de réglage global. Claude Code et ClickUp en auront.", 12, null, "Muted");
        none.TextWrapping = TextWrapping.Wrap;
        Body.Children.Add(Section("Réglages globaux"));
        Body.Children.Add(Card(none));
        Body.Children.Add(Section("Secrets (secrets.json, chiffrés avec ton compte Windows)"));
        Body.Children.Add(Card(_secrets));
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
            var remove = Btn("Effacer", () => Ctx.Editor.RemoveSecret(name), "GhostButton");
            DockPanel.SetDock(remove, Dock.Right);
            row.Children.Add(remove);
            var defined = Ui.Text("défini", 11, null, "Muted"); defined.Margin = new Thickness(12, 0, 12, 0); DockPanel.SetDock(defined, Dock.Right);
            row.Children.Add(defined);
            row.Children.Add(Ui.Text(name, 13));
            _secrets.Children.Add(row);
        }
    }
}

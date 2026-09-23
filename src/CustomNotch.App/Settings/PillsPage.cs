using System.Windows;
using System.Windows.Controls;
using CustomNotch.Core.Config;

namespace CustomNotch.App.Settings;

/// <summary>Maître-détail : à gauche l'arbre pilule → cellules (une liste indentée), à droite l'éditeur de l'élément
/// choisi. Toute opération passe par ConfigEditor ; une configuration refusée s'affiche dans le bandeau et rien n'est
/// appliqué. La sélection survit au rafraîchissement.</summary>
public sealed class PillsPage : PageBase
{
    private readonly ListBox _tree = new();
    private readonly ContentControl _editor = new();
    private readonly EditorBanner _banner = new();
    private readonly Button _hide;
    private (string Kind, string Id)? _selected;
    /// <summary>Vrai pendant que Refresh() reconstruit l'arbre et y restaure la sélection : le gestionnaire de
    /// SelectionChanged se tait alors, pour que Refresh() reste la seule à décider de reconstruire l'éditeur
    /// (un seul ShowEditor() par rafraîchissement, jamais deux).</summary>
    private bool _refreshing;

    public PillsPage(SettingsContext ctx) : base(ctx, "Pilules & cellules", "Chaque pilule est ancrée à un bord ; ses cellules lisent une source. Tout s'applique immédiatement.")
    {
        _tree.SetResourceReference(StyleProperty, "TreeList");
        _tree.SelectionChanged += (_, _) =>
        {
            if (_refreshing) return;
            if (_tree.SelectedItem is ListBoxItem it && it.Tag is (string kind, string id)) { _selected = (kind, id); ShowEditor(); }
        };

        var tools = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        tools.Children.Add(Btn("+ Pilule", () => Try(() => Select("pill", Ctx.Editor.AddPill("right")))));
        tools.Children.Add(Btn("+ Cellule", AddCell, "Primary"));
        tools.Children.Add(Btn("↑", () => Move(-1)));
        tools.Children.Add(Btn("↓", () => Move(+1)));
        _hide = Btn("Masquer", ToggleVisible);
        tools.Children.Add(_hide);
        tools.Children.Add(Btn("Supprimer", Remove, "GhostButton"));

        var left = new DockPanel { Width = 300, Margin = new Thickness(0, 0, 20, 0) };
        DockPanel.SetDock(tools, Dock.Top);
        left.Children.Add(tools);
        left.Children.Add(Card(_tree));

        var right = new StackPanel();
        right.Children.Add(_banner);
        right.Children.Add(_editor);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(left);
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        Body.Children.Add(grid);
        OnStoreChanged(Refresh);
    }

    /// <summary>Exécute une opération de l'éditeur ; un refus s'affiche, une réussite efface le bandeau.</summary>
    public void Try(Action op)
    {
        try { op(); _banner.Clear(); }
        catch (ConfigException ex) { _banner.Show(ex.Message); }
    }

    public void Select(string kind, string id)
    {
        _selected = (kind, id);
        Refresh();
    }

    /// <summary>Reconstruit l'arbre depuis le fichier et y restaure la sélection, puis reconstruit l'éditeur — une
    /// seule fois, que la sélection ait changé ou non (l'éditeur doit refléter le fichier). Poser _tree.SelectedItem
    /// ici déclenche SelectionChanged, mais _refreshing le fait taire : sinon l'éditeur serait construit une
    /// première fois par ce déclenchement, puis une seconde par l'appel explicite à ShowEditor() ci-dessous.</summary>
    public override void Refresh()
    {
        var file = Ctx.Store.Current;
        _refreshing = true;
        try
        {
            _tree.Items.Clear();
            var groups = file.AllCells().Where(c => c.IsGroup).ToList();
            var childIds = groups.SelectMany(g => g.Children!).ToHashSet();
            foreach (var pill in file.Pills)
            {
                _tree.Items.Add(Item("pill", pill.Id, $"Pilule « {pill.Id} »  ·  {Edge(pill.Edge)}", 0, pill.Visible, null, true));
                foreach (var cell in pill.Cells.Where(c => !childIds.Contains(c.Id)))
                {
                    _tree.Items.Add(Item("cell", cell.Id, cell.Label ?? cell.Id, 1, cell.Visible, cell.Glyph, false));
                    if (cell.IsGroup)
                        foreach (var childId in cell.Children!)
                            if (file.Cell(childId) is { } child)
                                _tree.Items.Add(Item("cell", child.Id, child.Label ?? child.Id, 2, child.Visible, child.Glyph, false));
                }
            }
            if (_selected is { } sel)
            {
                var match = _tree.Items.OfType<ListBoxItem>().FirstOrDefault(i => i.Tag is (string k, string id) && k == sel.Kind && id == sel.Id);
                if (match is not null) _tree.SelectedItem = match; else _selected = null;
            }
            if (_selected is null && _tree.Items.Count > 0) _tree.SelectedIndex = 0;
            if (_tree.SelectedItem is ListBoxItem picked && picked.Tag is (string k2, string id2)) _selected = (k2, id2);
        }
        finally { _refreshing = false; }
        ShowEditor();
    }

    private static string Edge(string edge) => edge switch { "left" => "gauche", "top" => "haut", "bottom" => "bas", _ => "droite" };

    private static ListBoxItem Item(string kind, string id, string text, int depth, bool visible, string? glyph, bool bold)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(depth * 18, 0, 0, 0) };
        if (glyph is not null)
        {
            var (data, filled) = Cells.GlyphLibrary.Get(glyph);
            var brush = new System.Windows.Media.SolidColorBrush(Cells.StatusPalette.Ink);
            var icon = filled ? Glyphs.Fill(data, 14, brush) : Glyphs.Stroke(data, 14, brush, 1.5);
            icon.Margin = new Thickness(0, 0, 8, 0);
            row.Children.Add(icon);
        }
        var label = Ui.Text(text, 13, bold ? FontWeights.SemiBold : FontWeights.Normal, visible ? "Fg" : "Muted");
        row.Children.Add(label);
        return new ListBoxItem { Content = row, Tag = (kind, id), ToolTip = visible ? null : "masquée sur ce poste" };
    }

    private void ShowEditor()
    {
        var file = Ctx.Store.Current;
        if (_selected is not { } sel)
        {
            _editor.Content = Ui.Text("Choisis une pilule ou une cellule.", 13, null, "Muted");
            _hide.Content = "Masquer";
            _hide.IsEnabled = false;
            return;
        }
        _hide.IsEnabled = true;
        if (sel.Kind == "pill")
        {
            var pill = file.Pills.FirstOrDefault(p => p.Id == sel.Id);
            _editor.Content = pill is null ? null : new PillEditor(Ctx, pill, Try);
            _hide.Content = pill?.Visible == false ? "Afficher" : "Masquer";
        }
        else
        {
            var cell = file.Cell(sel.Id);
            _editor.Content = cell is null ? null : new CellEditor(Ctx, cell, Try);
            _hide.Content = cell?.Visible == false ? "Afficher" : "Masquer";
        }
    }

    private string? PillOfSelection()
    {
        if (_selected is not { } sel) return null;
        if (sel.Kind == "pill") return sel.Id;
        return Ctx.Store.Current.Pills.FirstOrDefault(p => p.Cells.Any(c => c.Id == sel.Id))?.Id;
    }

    private void AddCell()
    {
        var pillId = PillOfSelection() ?? Ctx.Store.Current.Pills.FirstOrDefault()?.Id;
        if (pillId is null) { _banner.Show("Ajoute d'abord une pilule."); return; }
        var type = SourceCatalogDialog.Pick(Window.GetWindow(this)!, Ctx.Registry);
        if (type is null) return;
        Try(() => Select("cell", Ctx.Editor.AddCell(pillId, type)));
    }

    private void Move(int delta)
    {
        if (_selected is not { } sel) return;
        Try(() => { if (sel.Kind == "pill") Ctx.Editor.MovePill(sel.Id, delta); else Ctx.Editor.MoveCell(sel.Id, delta); });
    }

    private void ToggleVisible()
    {
        if (_selected is not { } sel) return;
        var file = Ctx.Store.Current;
        Try(() =>
        {
            if (sel.Kind == "pill") { var v = file.Pills.First(p => p.Id == sel.Id).Visible; Ctx.Editor.SetPillLocal(sel.Id, p => p["visible"] = !v); }
            else Ctx.Editor.SetCellVisible(sel.Id, !file.Cell(sel.Id)!.Visible);
        });
    }

    private void Remove()
    {
        if (_selected is not { } sel) return;
        var what = sel.Kind == "pill" ? $"la pilule « {sel.Id} » et ses cellules" : $"la cellule « {sel.Id} »";
        if (MessageBox.Show(Window.GetWindow(this), $"Supprimer {what} ?", "customNotch", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        Try(() => { if (sel.Kind == "pill") Ctx.Editor.RemovePill(sel.Id); else Ctx.Editor.RemoveCell(sel.Id); _selected = null; });
    }
}

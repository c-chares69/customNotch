using System.Windows;
using System.Windows.Controls;
using CustomNotch.Core.Sources;

namespace CustomNotch.App.Settings;

/// <summary>Le catalogue des sources : titre et description, filtrable. Rend le type choisi, ou null.</summary>
public sealed class SourceCatalogDialog : Window
{
    private readonly ListBox _list = new();
    private readonly List<SourceSchema> _all;
    private string? _result;

    private SourceCatalogDialog(Window owner, SourceRegistry registry)
    {
        Owner = owner;
        Title = "Ajouter une cellule";
        Width = 520; Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        SetResourceReference(BackgroundProperty, "Bg");
        FontFamily = (System.Windows.Media.FontFamily)FindResource("UiFont");
        _all = registry.Schemas.Values.OrderBy(s => s.Title).ToList();

        var filter = new TextBox { Margin = new Thickness(0, 0, 0, 10), Padding = new Thickness(10, 7, 10, 7) };
        filter.SetResourceReference(StyleProperty, "Field");
        filter.TextChanged += (_, _) => Fill(filter.Text);
        _list.SetResourceReference(StyleProperty, "TreeList");
        _list.MouseDoubleClick += (_, _) => Accept();
        var ok = new Button { Content = "Ajouter", MinWidth = 100, Margin = new Thickness(8, 0, 0, 0), IsDefault = true }; ok.SetResourceReference(StyleProperty, "Primary"); ok.Click += (_, _) => Accept();
        var cancel = new Button { Content = "Annuler", MinWidth = 100, IsCancel = true }; cancel.SetResourceReference(StyleProperty, "Secondary");
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        buttons.Children.Add(cancel); buttons.Children.Add(ok);
        var root = new Grid { Margin = new Thickness(20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(filter);
        Grid.SetRow(_list, 1); root.Children.Add(_list);
        Grid.SetRow(buttons, 2); root.Children.Add(buttons);
        Content = root;
        SourceInitialized += (_, _) => Theme.ApplyChrome(this);
        Fill("");
        Loaded += (_, _) => filter.Focus();
    }

    private void Fill(string filter)
    {
        _list.Items.Clear();
        foreach (var s in _all.Where(s => filter.Length == 0 || s.Title.Contains(filter, StringComparison.OrdinalIgnoreCase) || (s.Description ?? "").Contains(filter, StringComparison.OrdinalIgnoreCase) || s.Type.Contains(filter, StringComparison.OrdinalIgnoreCase)))
        {
            var row = new StackPanel { Margin = new Thickness(4) };
            row.Children.Add(Ui.Text(s.Title, 13, FontWeights.SemiBold));
            var d = Ui.Text(s.Description ?? s.Type, 11, null, "Muted"); d.TextWrapping = TextWrapping.Wrap;
            row.Children.Add(d);
            _list.Items.Add(new ListBoxItem { Content = row, Tag = s.Type });
        }
        if (_list.Items.Count > 0) _list.SelectedIndex = 0;
    }

    private void Accept()
    {
        if (_list.SelectedItem is not ListBoxItem it) return;
        _result = (string)it.Tag;
        DialogResult = true;
    }

    public static string? Pick(Window owner, SourceRegistry registry)
    {
        var dialog = new SourceCatalogDialog(owner, registry);
        return dialog.ShowDialog() == true ? dialog._result : null;
    }
}

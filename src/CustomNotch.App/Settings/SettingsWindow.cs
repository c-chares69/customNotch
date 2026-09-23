using System.Windows;
using System.Windows.Controls;

namespace CustomNotch.App.Settings;

/// <summary>La fenêtre Réglages : sidebar à gauche, page à droite, « Fermer » en bas. Une seule instance, tenue par le
/// contrôleur ; chaque page applique ses changements immédiatement (pas de bouton Enregistrer, comme dans les autres apps).</summary>
public sealed class SettingsWindow : Window
{
    public static readonly (string Key, string Label)[] Nav = { ("pills", "Pilules & cellules"), ("sources", "Sources"), ("claude", "Claude"), ("general", "Général") };

    private readonly ListBox _nav = new();
    private readonly ContentControl _host = new();
    private readonly Dictionary<string, PageBase> _pages = new();

    public SettingsWindow(SettingsContext ctx)
    {
        Title = "customNotch — Réglages";
        Width = 1040; Height = 720; MinWidth = 900; MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SetResourceReference(BackgroundProperty, "Bg");
        SetResourceReference(ForegroundProperty, "Fg");
        FontFamily = (System.Windows.Media.FontFamily)FindResource("UiFont");

        _pages["pills"] = new PillsPage(ctx);
        _pages["sources"] = new SourcesPage(ctx);
        _pages["claude"] = new ClaudePage(ctx);
        _pages["general"] = new GeneralPage(ctx);

        var side = new Grid { Width = 220 };
        side.SetResourceReference(BackgroundProperty, "Sidebar");
        side.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        side.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        side.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var brand = new StackPanel { Margin = new Thickness(16, 18, 16, 12) };
        brand.Children.Add(Ui.Text("customNotch", 20, FontWeights.SemiBold));
        brand.Children.Add(Ui.Text("Réglages", 11.5, null, "Muted"));
        side.Children.Add(brand);
        _nav.SetResourceReference(StyleProperty, "NavList");
        _nav.Margin = new Thickness(8, 0, 8, 0);
        foreach (var (key, label) in Nav) _nav.Items.Add(new ListBoxItem { Content = label, Tag = key });
        _nav.SelectionChanged += (_, _) => { if (_nav.SelectedItem is ListBoxItem it && it.Tag is string key) Show(key); };
        Grid.SetRow(_nav, 1);
        side.Children.Add(_nav);
        var version = Ui.Text($"version {Core.App.Version}", 11.5, null, "Muted");
        version.Margin = new Thickness(16, 8, 16, 16);
        Grid.SetRow(version, 2);
        side.Children.Add(version);

        var bottom = new Border { Padding = new Thickness(20, 10, 20, 10), BorderThickness = new Thickness(0, 1, 0, 0) };
        bottom.SetResourceReference(Border.BackgroundProperty, "Surface");
        bottom.SetResourceReference(Border.BorderBrushProperty, "Border");
        var close = new Button { Content = "Fermer", HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 100 };
        close.SetResourceReference(StyleProperty, "Primary");
        close.Click += (_, _) => Close();
        bottom.Child = close;

        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.Children.Add(_host);
        Grid.SetRow(bottom, 1);
        content.Children.Add(bottom);

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(side);
        Grid.SetColumn(content, 1);
        root.Children.Add(content);
        Content = root;

        SourceInitialized += (_, _) => Theme.ApplyChrome(this);
        Closed += (_, _) => { foreach (var p in _pages.Values) p.Detach(); };
        _nav.SelectedIndex = 0;
    }

    public void Go(string key) => _nav.SelectedIndex = Array.FindIndex(Nav, n => n.Key == key);

    private void Show(string key)
    {
        var page = _pages[key];
        page.Refresh();
        _host.Content = page;
    }
}

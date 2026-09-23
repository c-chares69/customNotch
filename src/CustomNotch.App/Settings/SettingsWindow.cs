using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CustomNotch.App.Settings;

/// <summary>La fenêtre Réglages, au gabarit de HubWindow.xaml (ClickUp-Extended) : sidebar 240 px (marque, puis
/// navigation à glyph — RadioButton style NavItem, IsChecked pilote la page montrée), page à droite (en-tête
/// titre 22 / sous-titre, défilement rembourré, pied « Fermer »). Une seule instance, tenue par le contrôleur ;
/// chaque page applique ses changements immédiatement (pas de bouton Enregistrer, comme dans les autres apps).</summary>
public sealed class SettingsWindow : Window
{
    /// <summary>Les entrées de navigation, dans l'ordre où elles apparaissent : les trois pages « métier » puis,
    /// sous l'intitulé SYSTÈME, Général — comme le plan le prescrit.</summary>
    public static readonly (string Key, string Label, Geometry Glyph)[] Nav =
    {
        ("pills", "Pilules & cellules", Glyphs.Layout),
        ("sources", "Sources", Glyphs.Link),
        ("claude", "Claude", Glyphs.Pulse),
        ("general", "Général", Glyphs.Gear),
    };

    private readonly List<RadioButton> _radios = new();
    private readonly ContentControl _host = new();
    private readonly Dictionary<string, PageBase> _pages = new();
    private readonly TextBlock _pageTitle = new() { FontSize = 22, FontWeight = FontWeights.SemiBold };
    private readonly TextBlock _pageSubtitle;

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

        _pageSubtitle = Bricks.Hint(this, "");
        _pageSubtitle.Margin = new Thickness(0, 4, 0, 0);

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(BuildSidebar());
        var content = BuildContent();
        Grid.SetColumn(content, 1);
        root.Children.Add(content);
        Content = root;

        SourceInitialized += (_, _) => Theme.ApplyChrome(this);
        Closed += (_, _) => { foreach (var p in _pages.Values) p.Detach(); };
        _radios[0].IsChecked = true;
    }

    /// <summary>La barre latérale : marque (tuile + nom), navigation à glyph, version en bas — comme le
    /// DockPanel de HubWindow.xaml (marque en haut, état en bas, navigation qui prend le reste).</summary>
    private Border BuildSidebar()
    {
        var dock = new DockPanel();

        var brand = new DockPanel { Margin = new Thickness(22, 22, 18, 8) };
        var tile = BuildBrandTile();
        DockPanel.SetDock(tile, Dock.Left);
        dock.Children.Add(brand);
        var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        names.Children.Add(Ui.Text("customNotch", 14, FontWeights.SemiBold, "Fg"));
        var sub = Ui.Text("Réglages", 11.5, null, "Subtle");
        sub.Margin = new Thickness(0, 1, 0, 0);
        names.Children.Add(sub);
        brand.Children.Add(tile);
        brand.Children.Add(names);
        DockPanel.SetDock(brand, Dock.Top);

        var version = Ui.Text($"version {Core.App.Version}", 11, null, "Subtle");
        version.Margin = new Thickness(24, 8, 20, 18);
        DockPanel.SetDock(version, Dock.Bottom);

        var nav = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        foreach (var (key, label, glyph) in Nav)
        {
            if (key == "general") nav.Children.Add(new TextBlock { Text = "SYSTÈME", Style = (Style)FindResource("NavSection") });
            var radio = new RadioButton { Content = label, Tag = glyph, Style = (Style)FindResource("NavItem") };
            radio.Checked += (_, _) => Show(key);
            _radios.Add(radio);
            nav.Children.Add(radio);
        }

        dock.Children.Add(version);
        dock.Children.Add(nav);

        var sidebar = new Border { Child = dock };
        sidebar.SetResourceReference(Border.BackgroundProperty, "Sidebar");
        sidebar.SetResourceReference(Border.BorderBrushProperty, "Border");
        sidebar.BorderThickness = new Thickness(0, 0, 1, 0);
        return sidebar;
    }

    /// <summary>La tuile de marque : 34×34, coins 9, dégradé sombre, portant le symbole de l'icône (le tracé de
    /// scripts/make-icon.ps1 : capsule blanche, anneau et deux barres) réduit à un trait blanc 16×14.</summary>
    private static Border BuildBrandTile()
    {
        var gradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0x2E, 0x30, 0x39), 0));
        gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0x0D, 0x0E, 0x12), 1));
        var mark = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M6,3 L10,3 A4,4 0 0 1 10,11 L6,11 A4,4 0 0 1 6,3 Z M5.5,7 A1.6,1.6 0 1 1 5.49,7 M8,5.8 L12,5.8 M8,8.2 L12,8.2"),
            Stroke = Brushes.White, StrokeThickness = 1.4, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round,
            Stretch = Stretch.Uniform, Width = 16, Height = 14, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
        return new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(9), Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center, Background = gradient, Child = mark };
    }

    /// <summary>La colonne de droite : en-tête (titre, sous-titre), défilement rembourré (36,12,18,24, comme
    /// HubWindow.xaml), pied « Fermer » — inchangé depuis avant cette tâche.</summary>
    private Grid BuildContent()
    {
        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel { Margin = new Thickness(36, 28, 36, 10) };
        header.Children.Add(_pageTitle);
        header.Children.Add(_pageSubtitle);
        content.Children.Add(header);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(36, 12, 18, 24), Content = _host };
        Grid.SetRow(scroll, 1);
        content.Children.Add(scroll);

        var bottom = new Border { Padding = new Thickness(20, 10, 20, 10), BorderThickness = new Thickness(0, 1, 0, 0) };
        bottom.SetResourceReference(Border.BackgroundProperty, "Surface");
        bottom.SetResourceReference(Border.BorderBrushProperty, "Border");
        var close = new Button { Content = "Fermer", HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 100 };
        close.SetResourceReference(StyleProperty, "Primary");
        close.Click += (_, _) => Close();
        bottom.Child = close;
        Grid.SetRow(bottom, 2);
        content.Children.Add(bottom);

        return content;
    }

    /// <summary>Sélectionne l'entrée de navigation portant cette clé — sa RadioButton passe à IsChecked, ce qui
    /// déclenche Show(key) comme un clic de l'utilisateur.</summary>
    public void Go(string key)
    {
        var index = Array.FindIndex(Nav, n => n.Key == key);
        if (index >= 0) _radios[index].IsChecked = true;
    }

    private void Show(string key)
    {
        var page = _pages[key];
        page.Refresh();
        _pageTitle.Text = page.Title;
        _pageSubtitle.Text = page.Subtitle;
        _pageSubtitle.Visibility = page.Subtitle.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        _host.Content = page;
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using CustomNotch.App.Cells;
using CustomNotch.Core.Model;

namespace CustomNotch.App.Notch;

/// <summary>La carte de codenotch, mise en page « B » : au thème Windows (Surface/Fg/Muted/Border, comme la
/// fenêtre Réglages — la pilule, elle, reste noire), 340 px de base, rayon 18, ombre portée, boutons à icônes.
/// L'échelle (Appearance.CardScale) se pose en LayoutTransform : la carte garde une largeur de base de 340 et des
/// polices à la taille de la spec, la transformation grandit tout le reste d'un coup. Ouverte 150 ms après
/// l'entrée, fermée 250 ms après la sortie, et gardée tant que le pointeur est dedans.</summary>
public sealed class HoverCard : Border
{
    /// <summary>La largeur de base (avant CardScale) : LayoutTransform grandit tout le reste, elle ne bouge
    /// jamais elle-même — sans quoi la mise à l'échelle s'appliquerait deux fois.</summary>
    private const double BaseWidth = 340;
    private const double BaseMinWidth = 240;
    private readonly IPillHost _host;
    private PillMetrics _m;
    private readonly StackPanel _stack = new();
    private readonly DispatcherTimer _hide = new() { Interval = TimeSpan.FromMilliseconds(250) };
    /// <summary>Fait avancer la barre de position d'une seconde à l'autre entre deux lectures de la session (qui ne
    /// publie sa position que de loin en loin) ; arrêté dès que la carte se ferme.</summary>
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(1) };
    private string? _cellId;
    private (long Pos, long Dur, long At, bool Playing)? _timeline;
    private Border? _posFill;
    private TextBlock? _posLeft;
    private TextBlock? _posRight;
    private double _posTrackWidth;

    public HoverCard(IPillHost host, PillMetrics m)
    {
        _host = host;
        _m = m;
        SetResourceReference(BackgroundProperty, "Surface");
        SetResourceReference(BorderBrushProperty, "Border");
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(18);
        Padding = new Thickness(20, 18, 20, 18);
        Effect = new DropShadowEffect { BlurRadius = 40, ShadowDepth = 12, Opacity = 0.45, Direction = 270 };
        // La base reste 340/240 quelle que soit l'échelle : LayoutTransform (ci-dessous) multiplie tout d'un coup,
        // deux fois sinon (PillMetrics.CardWidth est déjà à l'échelle, pour la réserve côté fenêtre).
        MaxWidth = BaseWidth;
        MinWidth = BaseMinWidth;
        LayoutTransform = new ScaleTransform(_m.CardScale, _m.CardScale);
        Visibility = Visibility.Collapsed;
        Child = new ScrollViewer { Content = _stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = 480 };
        MouseEnter += (_, _) => CancelHide();
        MouseLeave += (_, _) => HideLater();
        _hide.Tick += (_, _) => { _hide.Stop(); Hide(); };
        _tick.Tick += (_, _) => UpdateTimeline();
    }

    public string? CellId => _cellId;
    public bool IsOpen => Visibility == Visibility.Visible;

    /// <summary>Reprend de nouvelles métriques (l'échelle de la carte, réglable sans redémarrer) : la carte vit
    /// tout le cycle de la fenêtre et n'est jamais reconstruite, contrairement à ses lignes (Show, à chaque
    /// survol) — sans ce rappel depuis PillWindow.Apply, un changement d'Appearance.CardScale dans les Réglages
    /// restait sans effet tant que l'app ne redémarrait pas.</summary>
    public void Apply(PillMetrics m)
    {
        _m = m;
        MaxWidth = BaseWidth;
        MinWidth = BaseMinWidth;
        LayoutTransform = new ScaleTransform(_m.CardScale, _m.CardScale);
    }

    public void Show(CardModel model, string cellId)
    {
        _cellId = cellId;
        CancelHide();
        _tick.Stop();
        _timeline = null;
        _posFill = null; _posLeft = null; _posRight = null;
        _stack.Children.Clear();

        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
        var image = CoverImage.Decode(model.Image);
        if (image is not null)
        {
            var brush = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
            brush.Freeze();
            head.Children.Add(new Border { Width = 56, Height = 56, CornerRadius = new CornerRadius(12), Background = brush, VerticalAlignment = VerticalAlignment.Center });
        }
        else
        {
            var tile = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(10), VerticalAlignment = VerticalAlignment.Center, Child = Glyph(model.Glyph, 20, "Fg") };
            tile.SetResourceReference(BackgroundProperty, "SurfaceHover");
            head.Children.Add(tile);
        }
        var titles = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(Text(model.Title, 17, FontWeights.SemiBold, "Fg"));
        if (model.Subtitle is not null) titles.Children.Add(Text(model.Subtitle, 13, FontWeights.Normal, "Muted", new Thickness(0, 2, 0, 0)));
        head.Children.Add(titles);
        _stack.Children.Add(head);

        var first = true;
        foreach (var row in model.Rows)
        {
            _stack.Children.Add(Row(row, first));
            first = false;
        }

        if (model.Actions.Count > 0) _stack.Children.Add(Actions(model.Actions, cellId));
        if (model.Note is not null) _stack.Children.Add(Text(model.Note, 12, FontWeights.Normal, "Muted", new Thickness(0, 10, 0, 0), wrap: true));

        if (_timeline is not null) _tick.Start();
        Visibility = Visibility.Visible;
    }

    private static TextBlock Text(string text, double size, FontWeight weight, string brushKey, Thickness margin = default, bool wrap = false)
    {
        var block = new TextBlock
        {
            Text = text, FontSize = size, FontWeight = weight, Margin = margin,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        block.SetResourceReference(TextBlock.ForegroundProperty, brushKey);
        return block;
    }

    /// <summary>Le glyphe d'un id connu de GlyphLibrary (point par défaut sinon), teinté d'un pinceau du thème.</summary>
    private static UIElement Glyph(string? id, double size, string brushKey)
    {
        var (geometry, filled) = GlyphLibrary.Get(id);
        if (!filled) return Glyphs.Stroke(geometry, size, brushKey, 1.6);
        var path = Glyphs.Fill(geometry, size, Brushes.Transparent);
        path.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, brushKey);
        return path;
    }

    /// <summary>Une ligne : label 14 semi-gras à gauche, texte 13 gris à droite, barre 6 px si fraction, séparateur
    /// 1 px au-dessus (sauf la première). La ligne de position de lecture (hint « timeline:pos:dur:at ») n'a pas ce
    /// gabarit : pas de label, barre pleine largeur, temps à gauche et à droite — sauf hint malformé, où elle
    /// retombe sur le gabarit normal sans barre ni minuteur (une source qui se trompe ne doit jamais planter la
    /// carte).</summary>
    private UIElement Row(CardRow row, bool first)
    {
        var isTimeline = row.Hint is { } h && h.StartsWith("timeline:", StringComparison.Ordinal);
        UIElement content;
        if (isTimeline && ParseTimeline(row.Hint!, row.Tone) is { } t) content = TimelineContent(t);
        else if (isTimeline) content = NormalContent(row with { Fraction = null });
        else content = NormalContent(row);
        var wrapper = new Border { Padding = new Thickness(0, 9, 0, 9), Child = content };
        if (!first)
        {
            wrapper.BorderThickness = new Thickness(0, 1, 0, 0);
            wrapper.SetResourceReference(BorderBrushProperty, "Border");
        }
        return wrapper;
    }

    private static UIElement NormalContent(CardRow row)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var label = Text(row.Label, 14, FontWeights.SemiBold, "Fg");
        Grid.SetColumn(label, 0); Grid.SetRow(label, 0);
        grid.Children.Add(label);
        if (row.Text is { } text)
        {
            var value = Text(text, 13, FontWeights.Normal, "Muted");
            value.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(value, 1); Grid.SetRow(value, 0);
            grid.Children.Add(value);
        }
        if (row.Fraction is { } f)
        {
            grid.RowDefinitions.Add(new RowDefinition());
            grid.RowDefinitions.Add(new RowDefinition());
            var track = new Border { Height = 6, CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 6, 0, 0) };
            track.SetResourceReference(BackgroundProperty, "Track");
            var fill = new Border { Height = 6, CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Left, Background = StatusPalette.Brush(row.Tone) };
            track.SizeChanged += (_, e) => fill.Width = Math.Max(0, e.NewSize.Width * Math.Clamp(f, 0, 1));
            track.Child = fill;
            Grid.SetColumnSpan(track, 2); Grid.SetRow(track, 1);
            grid.Children.Add(track);
        }
        return grid;
    }

    /// <summary>« timeline:pos:dur:at » → le triplet et l'état de lecture, ou null si le hint n'a pas exactement
    /// quatre segments ou qu'un des trois nombres ne s'analyse pas — jamais d'exception, juste une ligne qui
    /// retombe sur le gabarit normal.</summary>
    private static (long Pos, long Dur, long At, bool Playing)? ParseTimeline(string hint, Status? tone)
    {
        var parts = hint.Split(':');
        if (parts.Length != 4) return null;
        if (!long.TryParse(parts[1], out var pos) || !long.TryParse(parts[2], out var dur) || !long.TryParse(parts[3], out var at)) return null;
        return (pos, dur, at, tone == Status.Busy);
    }

    /// <summary>La barre de position : plein largeur, couleur Busy (lecture), plus les deux temps dessous. Pose
    /// aussi l'état d'où le timer d'une seconde repart.</summary>
    private UIElement TimelineContent((long Pos, long Dur, long At, bool Playing) timeline)
    {
        _timeline = timeline;
        var panel = new StackPanel();
        var track = new Border { Height = 6, CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 0, 0, 6) };
        track.SetResourceReference(BackgroundProperty, "Track");
        _posFill = new Border { Height = 6, CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Left, Background = StatusPalette.Brush(Status.Busy) };
        track.SizeChanged += (_, e) => { _posTrackWidth = e.NewSize.Width; UpdateTimeline(); };
        track.Child = _posFill;
        panel.Children.Add(track);
        var times = new DockPanel { LastChildFill = false };
        _posLeft = Text("", 12, FontWeights.Normal, "Muted");
        _posRight = Text("", 12, FontWeights.Normal, "Muted");
        DockPanel.SetDock(_posLeft, Dock.Left);
        DockPanel.SetDock(_posRight, Dock.Right);
        times.Children.Add(_posLeft);
        times.Children.Add(_posRight);
        panel.Children.Add(times);
        UpdateTimeline();
        return panel;
    }

    /// <summary>Recalcule la position (avancée localement d'une seconde à l'autre tant que ça joue, bornée à la
    /// durée) et repeint la barre et les deux temps. En pause, la position ne bouge pas : c'est Tone qui l'a dit.</summary>
    private void UpdateTimeline()
    {
        if (_timeline is not { } t) return;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var pos = Math.Clamp(t.Playing ? t.Pos + (now - t.At) : t.Pos, 0, Math.Max(t.Dur, 0));
        if (_posFill is not null) _posFill.Width = t.Dur > 0 ? Math.Max(0, _posTrackWidth * pos / t.Dur) : 0;
        if (_posLeft is not null) _posLeft.Text = Clock(pos);
        if (_posRight is not null) _posRight.Text = Clock(t.Dur);
    }

    /// <summary>« 2:31 » : minutes sans zéro devant, secondes sur deux chiffres.</summary>
    private static string Clock(long ms) => $"{ms / 60000}:{ms / 1000 % 60:00}";

    /// <summary>Une ligne de boutons pleine largeur (WrapPanel) sauf quand toutes les actions sont sans libellé
    /// (les trois boutons média) : alors une seule ligne d'icônes centrées, celle du milieu (le bouton bascule)
    /// plus large.</summary>
    private UIElement Actions(IReadOnlyList<CardAction> actions, string cellId)
    {
        if (actions.All(a => string.IsNullOrEmpty(a.Label)))
        {
            var grid = new UniformGrid { Rows = 1, Columns = actions.Count, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 0) };
            for (var i = 0; i < actions.Count; i++)
            {
                var button = MakeButton(actions[i], cellId);
                button.Width = i == actions.Count / 2 ? 64 : 44;
                button.Height = 36;
                button.Margin = new Thickness(4, 0, 4, 0);
                grid.Children.Add(button);
            }
            return grid;
        }
        var wrap = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        foreach (var a in actions)
        {
            var button = MakeButton(a, cellId);
            button.Margin = new Thickness(0, 0, 8, 8);
            wrap.Children.Add(button);
        }
        return wrap;
    }

    private Button MakeButton(CardAction action, string cellId)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        content.Children.Add(Glyph(action.Icon, 16, "Fg"));
        if (!string.IsNullOrEmpty(action.Label)) content.Children.Add(Text(action.Label, 13, FontWeights.Normal, "Fg", new Thickness(8, 0, 0, 0)));
        var button = new Button { Content = content, Style = (Style)FindResource("CardButton") };
        var a = action;
        button.Click += async (_, _) =>
        {
            if (a.Config is not null) await _host.RunActionAsync(cellId, a.Config);
            // TargetCellId n'est posé que pour l'action d'un enfant de groupe : sinon (une action de la source de
            // la cellule elle-même) c'est cellId qu'il faut viser, jamais un découpage de l'id d'action lui-même
            // (qui peut légitimement contenir « : »).
            else if (a.SourceAction is { } sa) await _host.InvokeSourceAsync(a.TargetCellId ?? cellId, sa);
        };
        return button;
    }

    public void HideLater() { _hide.Stop(); _hide.Start(); }
    public void CancelHide() => _hide.Stop();

    public void Hide()
    {
        _cellId = null;
        // Les deux minuteurs de la carte : _tick (avance de la position affichée) et _hide (fermeture différée,
        // au cas où Hide() serait appelé pendant qu'elle compte déjà) — sans les arrêter tous les deux, l'un
        // continuait de tourner après la fermeture de la fenêtre qui la porte.
        _tick.Stop();
        _hide.Stop();
        Visibility = Visibility.Collapsed;
    }
}

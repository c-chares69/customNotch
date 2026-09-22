using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using CustomNotch.App.Cells;
using CustomNotch.Core.Model;

namespace CustomNotch.App.Notch;

/// <summary>La carte de codenotch : fond #0a0a0a, rayon 16, padding 16, largeur max 246 ; en-tête glyph + titre,
/// lignes label / indication à droite, barre 4 px, boutons. Ouverte 150 ms après l'entrée, fermée 250 ms après la
/// sortie, et gardée tant que le pointeur est dedans.</summary>
public sealed class HoverCard : Border
{
    private readonly IPillHost _host;
    private readonly PillMetrics _m;
    private readonly StackPanel _stack = new();
    private readonly DispatcherTimer _hide = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private string? _cellId;

    public HoverCard(IPillHost host, PillMetrics m)
    {
        _host = host;
        _m = m;
        Background = StatusPalette.Frozen(StatusPalette.CardBg);
        CornerRadius = new CornerRadius(16);
        Padding = new Thickness(16);
        MaxWidth = m.CardWidth;
        MinWidth = 160;
        Visibility = Visibility.Collapsed;
        Child = new ScrollViewer { Content = _stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = 400 };
        Tail = new Path { Fill = Background, Visibility = Visibility.Collapsed };
        MouseEnter += (_, _) => CancelHide();
        MouseLeave += (_, _) => HideLater();
        _hide.Tick += (_, _) => { _hide.Stop(); Hide(); };
    }

    /// <summary>La queue vers la cellule : un triangle 32 × 36, posé par la fenêtre à côté de la carte.</summary>
    public Path Tail { get; }
    public string? CellId => _cellId;
    public bool IsOpen => Visibility == Visibility.Visible;

    public void Show(CardModel model, string cellId)
    {
        _cellId = cellId;
        CancelHide();
        _stack.Children.Clear();
        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        var (glyph, filled) = GlyphLibrary.Get(model.Glyph);
        head.Children.Add(filled ? Glyphs.Fill(glyph, 16, StatusPalette.Frozen(StatusPalette.Ink)) : Glyphs.Stroke(glyph, 16, StatusPalette.Frozen(StatusPalette.Ink), 1.6));
        head.Children.Add(Text(model.Title, 15, FontWeights.SemiBold, Colors.White, new Thickness(8, 0, 0, 0)));
        _stack.Children.Add(head);
        if (model.Subtitle is not null) _stack.Children.Add(Text(model.Subtitle, 11, FontWeights.Normal, StatusPalette.Dim, new Thickness(0, -6, 0, 10)));
        foreach (var row in model.Rows) _stack.Children.Add(Row(row));
        if (model.Actions.Count > 0)
        {
            var wrap = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            foreach (var action in model.Actions)
            {
                var button = new Button { Content = action.Label, Style = (Style)FindResource("CardButton"), Margin = new Thickness(0, 0, 6, 6) };
                var a = action;
                button.Click += async (_, _) =>
                {
                    if (a.Config is not null) await _host.RunActionAsync(cellId, a.Config);
                    else if (a.SourceAction is { } sa)
                    {
                        var parts = sa.Split(':', 2);
                        await _host.InvokeSourceAsync(parts.Length == 2 ? parts[0] : cellId, parts[^1]);
                    }
                };
                wrap.Children.Add(button);
            }
            _stack.Children.Add(wrap);
        }
        if (model.Note is not null) _stack.Children.Add(Text(model.Note, 12, FontWeights.Normal, Color.FromRgb(0xc8, 0xc8, 0xc8), new Thickness(0, 8, 0, 0), wrap: true));
        Visibility = Visibility.Visible;
        Tail.Visibility = Visibility.Visible;
    }

    private static TextBlock Text(string text, double size, FontWeight weight, Color color, Thickness margin, bool wrap = false)
        => new() { Text = text, FontSize = size, FontWeight = weight, Foreground = StatusPalette.Frozen(color), Margin = margin, TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>Une ligne : libellé à gauche (gras 12), indication à droite (gris 11), barre 4 px si fraction, texte dessous.</summary>
    private static UIElement Row(CardRow row)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        var top = new DockPanel();
        var hint = Text(row.Hint ?? "", 11, FontWeights.Normal, StatusPalette.Dim, new Thickness(8, 0, 0, 0));
        DockPanel.SetDock(hint, Dock.Right);
        top.Children.Add(hint);
        top.Children.Add(Text(row.Label, 12, FontWeights.SemiBold, StatusPalette.Ink, new Thickness(0)));
        panel.Children.Add(top);
        if (row.Fraction is { } f)
        {
            var track = new Border { Height = 4, CornerRadius = new CornerRadius(2), Background = StatusPalette.Frozen(Color.FromRgb(0x2d, 0x2d, 0x2d)), Margin = new Thickness(0, 6, 0, 4) };
            var fill = new Border { Height = 4, CornerRadius = new CornerRadius(2), Background = StatusPalette.Brush(row.Tone), HorizontalAlignment = HorizontalAlignment.Left };
            track.SizeChanged += (_, e) => fill.Width = Math.Max(0, e.NewSize.Width * Math.Clamp(f, 0, 1));
            track.Child = fill;
            panel.Children.Add(track);
        }
        if (row.Text is not null) panel.Children.Add(Text(row.Text, 11, FontWeights.Normal, StatusPalette.Dim, new Thickness(0, row.Fraction is null ? 2 : 0, 0, 0)));
        return panel;
    }

    public void HideLater() { _hide.Stop(); _hide.Start(); }
    public void CancelHide() => _hide.Stop();

    public void Hide()
    {
        _cellId = null;
        Visibility = Visibility.Collapsed;
        Tail.Visibility = Visibility.Collapsed;
    }
}

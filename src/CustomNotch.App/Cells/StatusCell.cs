using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using CustomNotch.App.Notch;
using CustomNotch.Core.Model;

namespace CustomNotch.App.Cells;

/// <summary>Le glyph et une pastille de statut en bas à droite ; Off = glyph seul, gris. En forme carrée, la face
/// est un Border à coins arrondis (fond piste ou pochette) au lieu d'un disque ; les deux existent ensemble,
/// `Render` ne fait que basculer leur `Visibility`. La pastille de statut, elle, reste toujours ronde.</summary>
public sealed class StatusCell : CellFace
{
    private readonly Ellipse _disc = new() { Fill = StatusPalette.Frozen(StatusPalette.Track) };
    private readonly Border _square = new() { CornerRadius = new CornerRadius(0), Background = StatusPalette.Frozen(StatusPalette.Track), Visibility = Visibility.Collapsed };
    private readonly Ellipse _dot = new() { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, StrokeThickness = 2, Stroke = System.Windows.Media.Brushes.Black };
    private Path? _glyph;

    public StatusCell()
    {
        Children.Add(_disc);
        Children.Add(_square);
        Children.Add(_dot);
        Panel.SetZIndex(_dot, 5);
    }

    public override void Render(CellView view, PillMetrics m)
    {
        var square = view.Shape == "square";
        _disc.Visibility = square ? Visibility.Collapsed : Visibility.Visible;
        _square.Visibility = square ? Visibility.Visible : Visibility.Collapsed;
        _disc.Width = _disc.Height = m.Ring;
        _square.Width = _square.Height = m.Ring;
        _square.CornerRadius = new CornerRadius(SquareArc.Radius(m.Ring));
        _dot.Width = _dot.Height = 11 * m.Scale;
        _dot.Fill = StatusPalette.Brush(view.Status);
        _dot.Visibility = view.Status == Status.Off ? Visibility.Collapsed : Visibility.Visible;
        if (_glyph is not null) Children.Remove(_glyph);
        var image = CoverImage.Decode(view.Image);
        var trackBrush = StatusPalette.Frozen(StatusPalette.Track);
        if (image is not null)
        {
            var brush = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
            brush.Freeze();
            _disc.Fill = brush;
            _square.Background = brush;
            _glyph = null;
        }
        else
        {
            _disc.Fill = trackBrush;
            _square.Background = trackBrush;
            _glyph = Glyph(view.Glyph, m, view.Stale || view.Status == Status.Off);
            Children.Add(_glyph);
        }
        var stale = image is not null && (view.Stale || view.Status == Status.Off);
        _disc.Opacity = stale ? 0.55 : 1;
        _square.Opacity = stale ? 0.55 : 1;
        Activity(view.Status, m, view.Activity, view.Shape);
    }
}

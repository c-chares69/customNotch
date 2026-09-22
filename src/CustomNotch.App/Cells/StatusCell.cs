using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using CustomNotch.App.Notch;
using CustomNotch.Core.Model;

namespace CustomNotch.App.Cells;

/// <summary>Le glyph et une pastille de statut en bas à droite ; Off = glyph seul, gris.</summary>
public sealed class StatusCell : CellFace
{
    private readonly Ellipse _disc = new() { Fill = StatusPalette.Frozen(StatusPalette.Track) };
    private readonly Ellipse _dot = new() { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, StrokeThickness = 2, Stroke = System.Windows.Media.Brushes.Black };
    private Path? _glyph;

    public StatusCell()
    {
        Children.Add(_disc);
        Children.Add(_dot);
        Panel.SetZIndex(_dot, 5);
    }

    public override void Render(CellView view, PillMetrics m)
    {
        _disc.Width = _disc.Height = m.Ring;
        _dot.Width = _dot.Height = 11 * m.Scale;
        _dot.Fill = StatusPalette.Brush(view.Status);
        _dot.Visibility = view.Status == Status.Off ? Visibility.Collapsed : Visibility.Visible;
        if (_glyph is not null) Children.Remove(_glyph);
        _glyph = Glyph(view.Glyph, m, view.Stale || view.Status == Status.Off);
        Children.Add(_glyph);
        Activity(view.Status, m);
    }
}

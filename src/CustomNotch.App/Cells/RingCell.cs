using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using CustomNotch.App.Notch;
using CustomNotch.Core.Model;

namespace CustomNotch.App.Cells;

/// <summary>L'anneau de codenotch : piste grise, arc coloré par le statut, glyph au centre.</summary>
public sealed class RingCell : CellFace
{
    private readonly Ellipse _track = new() { Stroke = StatusPalette.Frozen(StatusPalette.Track), StrokeThickness = 5 };
    private readonly Path _arc = new() { StrokeThickness = 5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    private Path? _glyph;

    public RingCell()
    {
        Children.Add(_track);
        Children.Add(_arc);
    }

    public override void Render(CellView view, PillMetrics m)
    {
        var stroke = 5 * m.Scale;
        var r = m.Ring / 2 - stroke / 2;
        _track.Width = _track.Height = 2 * r + stroke; _track.StrokeThickness = stroke;
        _arc.StrokeThickness = stroke;
        _arc.Width = _arc.Height = 2 * r;
        _arc.Data = RingArc.Geometry(view.Fraction ?? 0, r);
        _arc.Stroke = StatusPalette.Brush(view.Status is Status.Busy or Status.Attention ? Status.Ok : view.Status);
        _arc.Opacity = view.Stale ? 0.55 : 1;
        _track.Opacity = view.Stale ? 0.55 : 1;
        if (_glyph is not null) Children.Remove(_glyph);
        _glyph = Glyph(view.Glyph, m, view.Stale);
        Children.Add(_glyph);
        Activity(view.Status, m);
    }
}

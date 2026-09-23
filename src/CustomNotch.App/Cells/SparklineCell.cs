using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using CustomNotch.App.Notch;
using CustomNotch.Core.Model;

namespace CustomNotch.App.Cells;

/// <summary>Les 30 derniers points, mis à l'échelle du maximum observé, dans le carré de la cellule.</summary>
public sealed class SparklineCell : CellFace
{
    private readonly Ellipse _disc = new() { Fill = StatusPalette.Frozen(StatusPalette.Track) };
    private readonly Polyline _line = new() { StrokeThickness = 2, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };

    public SparklineCell()
    {
        Children.Add(_disc);
        Children.Add(_line);
    }

    public override void Render(CellView view, PillMetrics m)
    {
        _disc.Width = _disc.Height = m.Ring;
        _line.Stroke = StatusPalette.Brush(view.Status == Status.Off ? Status.Ok : view.Status);
        _line.Opacity = view.Stale ? 0.55 : 1;
        var points = (view.Reading.History ?? Array.Empty<(long, double)>()).TakeLast(30).Select(p => p.V).ToList();
        var inset = 9 * m.Scale;
        var w = m.Ring - 2 * inset;
        var h = m.Ring - 2 * inset;
        var max = Math.Max(points.Count > 0 ? points.Max() : 1, 1e-9);
        _line.Points = new PointCollection(points.Select((v, i) => new Point(inset + (points.Count > 1 ? i * w / (points.Count - 1) : w / 2), inset + h - v / max * h)));
        Activity(view.Status, m, view.Activity, view.Shape);
    }
}

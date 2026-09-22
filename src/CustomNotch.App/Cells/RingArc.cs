using System.Windows;
using System.Windows.Media;

namespace CustomNotch.App.Cells;

/// <summary>L'arc d'un anneau : part de midi, tourne dans le sens horaire, dans un carré de côté 2·radius.
/// À 100 % on s'arrête à 359,9° : un arc complet a le même point de départ et d'arrivée et WPF ne le trace pas.</summary>
public static class RingArc
{
    public static Geometry Geometry(double fraction, double radius, double startDeg = -90)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        if (fraction <= 0) return System.Windows.Media.Geometry.Empty;
        var sweep = Math.Min(fraction * 360, 359.9);
        var center = new Point(radius, radius);
        Point At(double deg)
        {
            var rad = deg * Math.PI / 180;
            return new Point(center.X + radius * Math.Cos(rad), center.Y + radius * Math.Sin(rad));
        }
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(At(startDeg), false, false);
            ctx.ArcTo(At(startDeg + sweep), new Size(radius, radius), 0, sweep > 180, SweepDirection.Clockwise, true, false);
        }
        g.Freeze();
        return g;
    }
}

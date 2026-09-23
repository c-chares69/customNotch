using System.Windows;
using System.Windows.Media;

namespace CustomNotch.App.Cells;

/// <summary>Le contour d'un carré à coins arrondis (côté size, rayon radius), parcouru dans le sens horaire depuis
/// le milieu du bord haut — même origine que RingArc, pour que la piste et l'arc carrés commencent au même endroit
/// visuel que leurs équivalents ronds. Le bord haut est coupé en deux : une première moitié pour partir du milieu,
/// une dernière moitié qui referme la boucle après le dernier coin. Neuf tronçons en tout : demi-bord, coin, bord,
/// coin, bord, coin, bord, coin, demi-bord.</summary>
public static class SquareArc
{
    /// <summary>Un tronçon du contour : une droite (Start→EndOrCenter) ou un quart de cercle (centré en
    /// EndOrCenter, de StartDeg à StartDeg+90, sens horaire — même convention d'angle que RingArc).</summary>
    private readonly record struct Segment(double Length, bool IsArc, Point Start, Point EndOrCenter, double StartDeg);

    public static double Perimeter(double size, double radius) => 4 * (size - 2 * radius) + 2 * Math.PI * radius;

    /// <summary>Le point atteint après avoir parcouru fraction × Perimeter le long du contour.</summary>
    public static Point PointAt(double fraction, double size, double radius)
    {
        var segments = Build(size, radius);
        var remaining = Math.Clamp(fraction, 0, 1) * Perimeter(size, radius);
        for (var i = 0; i < segments.Length; i++)
        {
            var s = segments[i];
            var t = s.Length <= 0 ? 1 : Math.Min(1, remaining / s.Length);
            if (remaining <= s.Length || i == segments.Length - 1) return PointOn(s, radius, t);
            remaining -= s.Length;
        }
        return PointOn(segments[^1], radius, 1);
    }

    /// <summary>Le contour, figé, du milieu du bord haut jusqu'à fraction × Perimeter (sens horaire). 0 → vide ;
    /// 1 → 99,9 % du tour, pour la même raison que RingArc : un contour tout juste bouclé a le même point de départ
    /// et d'arrivée, et WPF ne le distingue pas d'un tracé vide.</summary>
    public static Geometry Geometry(double fraction, double size, double radius)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        if (fraction <= 0) return System.Windows.Media.Geometry.Empty;
        if (fraction >= 1) fraction = 0.999;

        var segments = Build(size, radius);
        var remaining = fraction * Perimeter(size, radius);
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(segments[0].Start, false, false);
            foreach (var s in segments)
            {
                if (remaining <= 0) break;
                var t = s.Length <= 0 ? 1 : Math.Min(1, remaining / s.Length);
                var end = PointOn(s, radius, t);
                if (s.IsArc) ctx.ArcTo(end, new Size(radius, radius), 0, false, SweepDirection.Clockwise, true, false);
                else ctx.LineTo(end, true, false);
                remaining -= s.Length;
            }
        }
        g.Freeze();
        return g;
    }

    /// <summary>Le point à mi-chemin (t ∈ [0,1]) d'un tronçon : une interpolation linéaire sur une droite, un angle
    /// qui avance de t × 90° sur un arc.</summary>
    private static Point PointOn(Segment s, double radius, double t) => s.IsArc
        ? ArcPoint(s.EndOrCenter, radius, s.StartDeg + t * 90)
        : new Point(s.Start.X + (s.EndOrCenter.X - s.Start.X) * t, s.Start.Y + (s.EndOrCenter.Y - s.Start.Y) * t);

    private static Point ArcPoint(Point center, double radius, double deg)
    {
        var rad = deg * Math.PI / 180;
        return new Point(center.X + radius * Math.Cos(rad), center.Y + radius * Math.Sin(rad));
    }

    /// <summary>Les neuf tronçons, dans l'ordre horaire depuis (size/2, 0).</summary>
    private static Segment[] Build(double size, double radius)
    {
        var half = size / 2 - radius;
        var side = size - 2 * radius;
        var arc = Math.PI * radius / 2;

        var p0 = new Point(size / 2, 0);
        var p1 = new Point(size - radius, 0);
        var c1 = new Point(size - radius, radius);
        var p2 = ArcPoint(c1, radius, 0);
        var p3 = new Point(size, size - radius);
        var c2 = new Point(size - radius, size - radius);
        var p4 = ArcPoint(c2, radius, 90);
        var p5 = new Point(radius, size);
        var c3 = new Point(radius, size - radius);
        var p6 = ArcPoint(c3, radius, 180);
        var p7 = new Point(0, radius);
        var c4 = new Point(radius, radius);
        var p8 = ArcPoint(c4, radius, 270);

        return
        [
            new Segment(half, false, p0, p1, 0),            // demi-bord haut droit
            new Segment(arc, true, p1, c1, -90),             // coin haut-droit
            new Segment(side, false, p2, p3, 0),             // bord droit
            new Segment(arc, true, p3, c2, 0),               // coin bas-droit
            new Segment(side, false, p4, p5, 0),             // bord bas
            new Segment(arc, true, p5, c3, 90),              // coin bas-gauche
            new Segment(side, false, p6, p7, 0),             // bord gauche
            new Segment(arc, true, p7, c4, 180),             // coin haut-gauche
            new Segment(half, false, p8, p0, 0),             // demi-bord haut gauche (fermeture)
        ];
    }
}

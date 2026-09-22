using System.Windows;
using System.Windows.Media;

namespace CustomNotch.App.Notch;

/// <summary>La silhouette : un corps aux coins arrondis côté libre, et deux fillets inversés côté écran qui la
/// raccordent au bord - le carré au-dessus du corps moins le quart de cercle, comme le ::before de codenotch.
/// Dessinée pour le bord droit, puis retournée pour les autres.</summary>
public static class PillShape
{
    public static Geometry Build(PillMetrics m, string edge, int cells)
    {
        var w = m.Width;
        var f = m.Fillet;
        var c = m.Corner;
        var length = m.Length(cells);
        var extent = m.Extent(cells);
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(new Point(w, 0), isFilled: true, isClosed: true);
            // Oreille haute : du bord à la naissance du corps, en creux (petit arc, sens horaire à l'écran)
            ctx.ArcTo(new Point(w - f, f), new Size(f, f), 0, false, SweepDirection.Clockwise, true, false);
            ctx.LineTo(new Point(c, f), true, false);
            // Coin libre haut (convexe)
            ctx.ArcTo(new Point(0, f + c), new Size(c, c), 0, false, SweepDirection.Counterclockwise, true, false);
            ctx.LineTo(new Point(0, f + length - c), true, false);
            // Coin libre bas
            ctx.ArcTo(new Point(c, f + length), new Size(c, c), 0, false, SweepDirection.Counterclockwise, true, false);
            ctx.LineTo(new Point(w - f, f + length), true, false);
            // Oreille basse
            ctx.ArcTo(new Point(w, extent), new Size(f, f), 0, false, SweepDirection.Clockwise, true, false);
            ctx.LineTo(new Point(w, 0), true, false);
        }
        g.Transform = edge switch
        {
            "left" => new MatrixTransform(-1, 0, 0, 1, w, 0),
            "top" => new MatrixTransform(0, -1, 1, 0, 0, w),      // (x,y) → (y, w − x) : le bord x=w devient y=0
            "bottom" => new MatrixTransform(0, 1, -1, 0, extent, 0),   // (x,y) → (extent − y, x) : le bord devient y=w
            _ => Transform.Identity,
        };
        g.Freeze();
        return g;
    }
}

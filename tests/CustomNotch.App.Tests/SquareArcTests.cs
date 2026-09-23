using System.Windows;
using CustomNotch.App.Cells;
using Xunit;
namespace CustomNotch.App.Tests;

public class SquareArcTests
{
    [Fact] public void Zero_est_vide() => Assert.True(SquareArc.Geometry(0, 44, 12).IsEmpty());
    [Fact] public void Le_perimetre_est_celui_du_carre_arrondi() => Assert.Equal(4 * 20 + 2 * Math.PI * 12, SquareArc.Perimeter(44, 12), 6);
    [Fact]
    public void Un_quart_finit_au_milieu_du_bord_droit()
    {
        var g = SquareArc.Geometry(0.25, 44, 12);
        var end = SquareArc.PointAt(0.25, 44, 12);
        Assert.Equal(44, end.X, 3); Assert.Equal(22, end.Y, 3);
        Assert.False(g.IsEmpty());
    }
    [Fact]
    public void Un_demi_finit_au_milieu_du_bord_bas()
    {
        var end = SquareArc.PointAt(0.5, 44, 12);
        Assert.Equal(22, end.X, 3); Assert.Equal(44, end.Y, 3);
    }
}

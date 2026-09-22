using System.Windows;
using CustomNotch.App.Notch;
using Xunit;
namespace CustomNotch.App.Tests;

public class PillShapeTests
{
    private static readonly PillMetrics M = new(1.0);

    [Fact]
    public void Les_metriques_reprennent_codenotch()
    {
        Assert.Equal(70, M.Width);
        Assert.Equal(20, M.Corner);
        Assert.Equal(38.7, M.Fillet, 3);
        Assert.Equal(2 * 18 + 3 * 44 + 3 * 18 + 2 * 14, M.Length(3), 3);   // padding, 3 anneaux + 3 légendes, 2 espaces
        Assert.Equal(M.Length(3) + 2 * M.Fillet, M.Extent(3), 3);
    }

    [Theory]
    [InlineData("right")]
    [InlineData("left")]
    public void Un_bord_vertical_donne_un_canvas_largeur_x_extent(string edge)
    {
        var g = PillShape.Build(M, edge, 2);
        var b = g.Bounds;
        Assert.Equal(0, b.X, 1); Assert.Equal(0, b.Y, 1);
        Assert.Equal(M.Width, b.Width, 1);
        Assert.Equal(M.Extent(2), b.Height, 1);
    }

    [Theory]
    [InlineData("top")]
    [InlineData("bottom")]
    public void Un_bord_horizontal_est_tourne(string edge)
    {
        var b = PillShape.Build(M, edge, 2).Bounds;
        Assert.Equal(M.Extent(2), b.Width, 1);
        Assert.Equal(M.Width, b.Height, 1);
    }

    [Fact]
    public void Le_fillet_est_creux_et_l_oreille_pleine_bord_droit()
    {
        var g = PillShape.Build(M, "right", 1);
        var f = M.Fillet;
        Assert.True(g.FillContains(new Point(M.Width - 0.5, f - 0.5)), "le coin contre le bord, sous l'oreille, est noir");
        Assert.False(g.FillContains(new Point(M.Width - f + 0.5, 0.5)), "l'intérieur du quart de cercle est transparent");
        Assert.False(g.FillContains(new Point(0.5, f + 0.5)), "le coin libre est arrondi");
        Assert.True(g.FillContains(new Point(M.Width / 2, f + M.Length(1) / 2)), "le corps est plein");
    }

    [Fact]
    public void Le_bord_gauche_est_le_miroir()
    {
        var g = PillShape.Build(M, "left", 1);
        Assert.True(g.FillContains(new Point(0.5, M.Fillet - 0.5)));
        Assert.False(g.FillContains(new Point(M.Width - 0.5, M.Fillet + 0.5)));
    }
}

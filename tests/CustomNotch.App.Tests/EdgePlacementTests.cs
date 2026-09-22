using System.Windows;
using CustomNotch.App.Notch;
using Xunit;
namespace CustomNotch.App.Tests;

public class EdgePlacementTests
{
    private static readonly Rect Area = new(0, 0, 1920, 1040);   // zone de travail : barre des tâches en bas

    [Fact]
    public void A_droite_le_canvas_colle_au_bord_et_suit_along()
    {
        var r = EdgePlacement.Place(Area, "right", 0.5, extent: 300, thickness: 70);
        Assert.Equal(1920 - 70, r.X);
        Assert.Equal((1040 - 300) / 2.0, r.Y);
        Assert.Equal(70, r.Width); Assert.Equal(300, r.Height);
        Assert.Equal(0, EdgePlacement.Place(Area, "right", 0, 300, 70).Y);
        Assert.Equal(1040 - 300, EdgePlacement.Place(Area, "right", 1, 300, 70).Y);
    }

    [Fact]
    public void A_gauche_en_haut_en_bas()
    {
        Assert.Equal(0, EdgePlacement.Place(Area, "left", 0.5, 300, 70).X);
        var top = EdgePlacement.Place(Area, "top", 0.25, 300, 70);
        Assert.Equal(0, top.Y); Assert.Equal((1920 - 300) * 0.25, top.X); Assert.Equal(300, top.Width); Assert.Equal(70, top.Height);
        Assert.Equal(1040 - 70, EdgePlacement.Place(Area, "bottom", 0.5, 300, 70).Y);
    }

    [Fact]
    public void Along_se_retrouve_depuis_une_position()
    {
        var r = EdgePlacement.Place(Area, "right", 0.3, 300, 70);
        Assert.Equal(0.3, EdgePlacement.AlongFrom(Area, "right", r.Location, 300), 6);
        Assert.Equal(1, EdgePlacement.AlongFrom(Area, "top", new Point(5000, 0), 300));
        Assert.Equal(0, EdgePlacement.AlongFrom(Area, "top", new Point(-50, 0), 300));
    }

    [Fact]
    public void Un_ecran_decale_est_respecte()
    {
        var second = new Rect(1920, -200, 2560, 1440);
        var r = EdgePlacement.Place(second, "right", 0, 300, 70);
        Assert.Equal(1920 + 2560 - 70, r.X); Assert.Equal(-200, r.Y);
    }

    [Theory]
    [InlineData("right", 0)]
    [InlineData("right", 0.37)]
    [InlineData("right", 1)]
    [InlineData("left", 0)]
    [InlineData("left", 0.37)]
    [InlineData("left", 1)]
    [InlineData("top", 0)]
    [InlineData("top", 0.37)]
    [InlineData("top", 1)]
    [InlineData("bottom", 0)]
    [InlineData("bottom", 0.37)]
    [InlineData("bottom", 1)]
    public void Place_et_AlongFrom_sont_inverses(string edge, double along)
    {
        var placed = EdgePlacement.Place(Area, edge, along, extent: 300, thickness: 70);
        Assert.Equal(along, EdgePlacement.AlongFrom(Area, edge, placed.Location, 300), 6);
    }
}

using System.Windows;
using CustomNotch.App.Cells;
using Xunit;
namespace CustomNotch.App.Tests;

public class RingArcTests
{
    [Fact]
    public void Zero_ne_dessine_rien_et_un_dessine_presque_le_tour()
    {
        Assert.True(RingArc.Geometry(0, 20).IsEmpty());
        var full = RingArc.Geometry(1, 20).Bounds;
        Assert.Equal(40, full.Width, 0.5);
        Assert.Equal(40, full.Height, 0.5);
    }

    [Fact]
    public void Un_quart_part_de_midi_vers_trois_heures()
    {
        var b = RingArc.Geometry(0.25, 20).Bounds;
        Assert.Equal(20, b.Width, 0.5);   // de x=20 (midi) à x=40 (3 h)
        Assert.Equal(20, b.Height, 0.5);
        Assert.Equal(20, b.X, 0.5); Assert.Equal(0, b.Y, 0.5);
    }
}

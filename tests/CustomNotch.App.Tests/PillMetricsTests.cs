using CustomNotch.App.Notch;
using Xunit;
namespace CustomNotch.App.Tests;

public class PillMetricsTests
{
    [Fact]
    public void La_fenetre_reserve_la_hauteur_de_la_carte()
    {
        Assert.True(new PillMetrics(1.0, 1.5).WindowFloor >= 520 * 1.5 + 16);
        Assert.Equal(536, new PillMetrics(1.0, 1.0).WindowFloor);
    }
}

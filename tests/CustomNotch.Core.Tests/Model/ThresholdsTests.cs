using CustomNotch.Core.Model;
using Xunit;
namespace CustomNotch.Core.Tests.Model;

public class ThresholdsTests
{
    [Theory]
    [InlineData(10, Status.Ok)]
    [InlineData(50, Status.Warn)]
    [InlineData(74.9, Status.Warn)]
    [InlineData(75, Status.Crit)]
    public void Les_seuils_par_defaut_montent(double value, Status expected) => Assert.Equal(expected, Thresholds.RingDefault.Judge(value));

    [Theory]
    [InlineData(80, Status.Ok)]
    [InlineData(20, Status.Warn)]
    [InlineData(5, Status.Crit)]
    public void Inverses_bas_est_mauvais(double value, Status expected) => Assert.Equal(expected, new Thresholds(20, 5, Invert: true).Judge(value));

    [Fact]
    public void Sans_seuil_tout_est_ok() => Assert.Equal(Status.Ok, new Thresholds().Judge(999));
}

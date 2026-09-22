using CustomNotch.Core.Sources.System;
using Xunit;
namespace CustomNotch.Core.Tests.Sources;

public class UnitsTests
{
    [Fact]
    public void Les_octets_deviennent_des_gigaoctets_arrondis()
    {
        Assert.Equal(15.9, Units.Gb(17_070_000_000UL), 1);
        Assert.Equal(0, Units.Gb(0));
    }

    [Fact]
    public void Le_debit_choisit_son_unite()
    {
        Assert.Equal((512.0, "Ko/s"), Units.Rate(512 * 1024, 1));
        Assert.Equal((2.5, "Mo/s"), Units.Rate(5 * 1024 * 1024, 2));
        Assert.Equal((0.0, "Ko/s"), Units.Rate(0, 1));
    }

    [Fact]
    public void Le_cpu_est_la_part_non_inactive_du_temps()
    {
        var prev = (Idle: 100UL, Kernel: 200UL, User: 100UL);   // kernel inclut idle sous Windows
        var cur = (Idle: 150UL, Kernel: 300UL, User: 200UL);
        // Δkernel+Δuser = 200, Δidle = 50 → 75 %
        Assert.Equal(75, Units.CpuPercent(prev, cur), 1);
        Assert.Equal(0, Units.CpuPercent(prev, prev));
    }
}

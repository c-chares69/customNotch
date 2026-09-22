using CustomNotch.Core;
using Xunit;
namespace CustomNotch.Core.Tests;

public class PathsTests
{
    [Fact]
    public void Le_slug_machine_est_un_nom_de_fichier_sur()
    {
        Assert.Equal("pc-de-coco", Paths.MachineSlug("PC.DE:COCO"));
        Assert.Equal("machine", Paths.MachineSlug("..."));
    }

    [Fact]
    public void La_variable_d_environnement_l_emporte()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cn-" + Guid.NewGuid().ToString("N")[..8]);
        Environment.SetEnvironmentVariable(App.HomeEnv, dir);
        try { Assert.Equal(Path.GetFullPath(dir), Paths.Home()); }
        finally { Environment.SetEnvironmentVariable(App.HomeEnv, null); }
    }
}

using CustomNotch.Core.Platform;
using Xunit;
namespace CustomNotch.Core.Tests;

public class SingleInstanceTests
{
    [Fact]
    public void Le_marqueur_d_arret_volontaire_se_pose_et_se_leve()
    {
        var home = Path.Combine(Path.GetTempPath(), "customNotch-tests", Guid.NewGuid().ToString("N"));
        try
        {
            Assert.False(SingleInstance.StoppedByUser(home));
            SingleInstance.MarkStoppedByUser(home);
            Assert.True(SingleInstance.StoppedByUser(home));
            SingleInstance.ClearStoppedByUser(home);
            Assert.False(SingleInstance.StoppedByUser(home));
        }
        finally { if (Directory.Exists(home)) Directory.Delete(home, true); }
    }
}

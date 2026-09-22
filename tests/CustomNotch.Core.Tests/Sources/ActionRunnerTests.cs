using CustomNotch.Core.Actions;
using CustomNotch.Core.Config;
using Xunit;
namespace CustomNotch.Core.Tests.Sources;

public class ActionRunnerTests
{
    [Fact]
    public async Task Une_action_source_est_deleguee()
    {
        string? got = null;
        await ActionRunner.RunAsync(new ActionConfig { Source = "toggle" }, a => { got = a; return Task.CompletedTask; });
        Assert.Equal("toggle", got);
    }

    [Fact]
    public async Task Une_action_shell_s_execute_sans_fenetre()
    {
        var marker = Path.Combine(Path.GetTempPath(), "cn-" + Guid.NewGuid().ToString("N")[..8] + ".txt");
        await ActionRunner.RunAsync(new ActionConfig { Shell = $"echo ok > \"{marker}\"" }, _ => Task.CompletedTask);
        await Task.Delay(500);
        Assert.True(File.Exists(marker));
        File.Delete(marker);
    }

    [Fact]
    public async Task Une_action_vide_ne_fait_rien()
        => await ActionRunner.RunAsync(new ActionConfig(), _ => throw new InvalidOperationException("ne doit pas être appelé"));
}

using Xunit;
using CustomNotch.Core.Config;
using CustomNotch.Core.Model;
using CustomNotch.Core.Sources;
namespace CustomNotch.Core.Tests.Model;

public class CellClickResolverTests
{
    private static readonly SourceSchema Launcher = new("launcher", "Lanceur", Array.Empty<SchemaField>(), "link", "ouvre", "open");
    private static readonly SourceSchema Cpu = new("system.cpu", "Processeur", Array.Empty<SchemaField>(), "cpu", "cpu", null);

    [Fact]
    public void L_action_de_la_config_l_emporte()
    {
        var cell = new CellConfig { Id = "c", Source = "launcher", Actions = new CellActions { Click = new ActionConfig { Open = "https://x" } } };
        var plan = CellClickResolver.Resolve(cell, Launcher);
        Assert.Equal("https://x", plan.Config!.Open);
        Assert.Null(plan.SourceAction);
        Assert.False(plan.OpenCard);
    }

    [Fact]
    public void Sinon_l_action_par_defaut_de_la_source()
    {
        var plan = CellClickResolver.Resolve(new CellConfig { Id = "c", Source = "launcher" }, Launcher);
        Assert.Equal("open", plan.SourceAction);
        Assert.False(plan.OpenCard);
    }

    [Fact]
    public void Sinon_la_carte()
    {
        Assert.True(CellClickResolver.Resolve(new CellConfig { Id = "c", Source = "system.cpu" }, Cpu).OpenCard);
        Assert.True(CellClickResolver.Resolve(new CellConfig { Id = "c", Source = "zz" }, null).OpenCard);
    }

    [Fact]
    public void Un_groupe_ouvre_toujours_la_carte()
    {
        var group = new CellConfig { Id = "g", Children = new() { "a" } };
        Assert.True(CellClickResolver.Resolve(group, Launcher).OpenCard);
    }

    [Fact]
    public void Un_click_vide_dans_la_config_ne_compte_pas()
    {
        var cell = new CellConfig { Id = "c", Source = "launcher", Actions = new CellActions { Click = new ActionConfig() } };
        Assert.Equal("open", CellClickResolver.Resolve(cell, Launcher).SourceAction);
    }
}

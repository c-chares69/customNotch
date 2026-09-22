using System.Text.Json.Nodes;
using CustomNotch.Core.Model;
using CustomNotch.Core.Sources;
using CustomNotch.Core.Sources.System;
using Xunit;
namespace CustomNotch.Core.Tests.Sources;

public class SystemSourcesTests
{
    private static CellContext Ctx(string json = "{}") => new("c", JsonNode.Parse(json)!.AsObject(), null);

    [Fact]
    public async Task Le_cpu_rend_un_pourcentage_apres_deux_lectures()
    {
        var src = new CpuSource();
        await src.ReadAsync(Ctx(), CancellationToken.None);
        await Task.Delay(100);
        var r = await src.ReadAsync(Ctx(), CancellationToken.None);
        Assert.Equal(100, r.Max);
        Assert.InRange(r.Value!.Value, 0, 100);
    }

    [Fact]
    public async Task La_memoire_donne_utilise_sur_total_en_go()
    {
        var r = await new MemorySource().ReadAsync(Ctx(), CancellationToken.None);
        Assert.True(r.Max > 1);
        Assert.InRange(r.Value!.Value, 0, r.Max!.Value);
        Assert.Equal("Go", r.Unit);
        Assert.Contains(r.Detail!, d => d.Label == "Libre");
    }

    [Fact]
    public async Task Le_disque_lit_le_lecteur_demande_et_refuse_un_lecteur_absent()
    {
        var r = await new DiskSource().ReadAsync(Ctx("""{"drive":"C:"}"""), CancellationToken.None);
        Assert.True(r.Max > 0);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new DiskSource().ReadAsync(Ctx("""{"drive":"Q:"}"""), CancellationToken.None));
    }

    [Fact]
    public async Task Le_reseau_a_un_historique_et_une_unite()
    {
        var src = new NetworkSource();
        await src.ReadAsync(Ctx(), CancellationToken.None);
        await Task.Delay(50);
        var r = await src.ReadAsync(Ctx(), CancellationToken.None);
        Assert.NotNull(r.Value);
        Assert.Contains(r.Unit, new[] { "Ko/s", "Mo/s" });
        Assert.NotEmpty(r.History!);
    }

    [Fact]
    public async Task La_batterie_est_off_sans_batterie_sinon_un_anneau_inverse()
    {
        var r = await new BatterySource().ReadAsync(Ctx(), CancellationToken.None);
        if (r.Status == Status.Off) Assert.Null(r.Value);
        else { Assert.Equal(100, r.Max); Assert.InRange(r.Value!.Value, 0, 100); }
    }

    [Fact]
    public void Tout_est_enregistre()
    {
        var registry = new SourceRegistry();
        SystemSources.RegisterAll(registry);
        Assert.Superset(new HashSet<string> { "system.cpu", "system.memory", "system.disk", "system.network", "system.battery" }, registry.Types.ToHashSet());
    }
}

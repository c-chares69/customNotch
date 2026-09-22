using CustomNotch.Core.Config;
using CustomNotch.Core.Model;
using Xunit;
namespace CustomNotch.Core.Tests.Model;

public class GroupViewTests
{
    private static CellView Child(string id, Reading r, long now = 0)
        => CellViews.From(new CellConfig { Id = id, Label = id.ToUpperInvariant(), Glyph = id }, r, now);

    private static readonly CellConfig Group = new() { Id = "sys", Label = "Système", Glyph = "cpu", Children = new() { "cpu", "mem" } };

    [Fact]
    public void Sans_headline_le_pire_enfant_donne_l_anneau_et_le_statut()
    {
        var cpu = Child("cpu", new Reading(Value: 10, Max: 100));
        var mem = Child("mem", new Reading(Value: 90, Max: 100));
        var view = CellViews.FromGroup(Group, new[] { cpu, mem }, 0);
        Assert.Equal(CellKind.Group, view.Kind);
        Assert.Equal(Status.Crit, view.Status);
        Assert.Equal(0.9, view.Fraction);
        Assert.Equal("90%", view.Caption);
        Assert.Equal("cpu", view.Glyph);   // le glyph du groupe, pas celui de l'enfant
        Assert.Equal("Système", view.Label);
    }

    [Fact]
    public void Headline_choisit_l_enfant_de_l_anneau_mais_le_statut_reste_le_pire()
    {
        var cpu = Child("cpu", new Reading(Value: 10, Max: 100));
        var mem = Child("mem", new Reading(Value: 90, Max: 100));
        var g = new CellConfig { Id = "sys", Children = new() { "cpu", "mem" }, Headline = "cpu" };
        var view = CellViews.FromGroup(g, new[] { cpu, mem }, 0);
        Assert.Equal(0.1, view.Fraction);
        Assert.Equal(Status.Crit, view.Status);
    }

    [Fact]
    public void Busy_et_Attention_passent_devant_les_seuils()
    {
        var a = Child("a", new Reading(Value: 99, Max: 100));
        var b = Child("b", new Reading(Status: Status.Attention));
        Assert.Equal(Status.Attention, CellViews.FromGroup(Group, new[] { a, b }, 0).Status);
    }

    [Fact]
    public void Perime_seulement_si_tous_les_enfants_le_sont()
    {
        var fresh = Child("a", new Reading(Value: 1, Max: 2));
        var stale = Child("b", new Reading(Value: 1, Max: 2).AsStale(0, "x"), now: 120_000);
        Assert.False(CellViews.FromGroup(Group, new[] { fresh, stale }, 120_000).Stale);
        Assert.True(CellViews.FromGroup(Group, new[] { stale }, 120_000).Stale);
    }

    [Fact]
    public void Sans_enfant_le_groupe_est_off()
    {
        var view = CellViews.FromGroup(Group, Array.Empty<CellView>(), 0);
        Assert.Equal(Status.Off, view.Status);
        Assert.Null(view.Fraction);
    }
}

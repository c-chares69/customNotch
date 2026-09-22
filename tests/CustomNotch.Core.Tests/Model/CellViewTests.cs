using CustomNotch.Core.Config;
using CustomNotch.Core.Model;
using Xunit;
namespace CustomNotch.Core.Tests.Model;

public class CellViewTests
{
    private static CellConfig Cell(string? kind = null, Thresholds? t = null, bool group = false)
        => new() { Id = "c", Label = "CPU", Kind = kind, Thresholds = t, Children = group ? new List<string> { "a" } : null };

    [Fact]
    public void Un_maximum_donne_un_anneau()
        => Assert.Equal(CellKind.Ring, CellViews.DeriveKind(Cell(), new Reading(Value: 30, Max: 100)));

    [Fact]
    public void Un_historique_donne_une_sparkline()
        => Assert.Equal(CellKind.Sparkline, CellViews.DeriveKind(Cell(), new Reading(Value: 3, History: new[] { (1L, 1.0), (2L, 3.0) })));

    [Fact]
    public void Une_valeur_seule_donne_une_valeur()
        => Assert.Equal(CellKind.Value, CellViews.DeriveKind(Cell(), new Reading(Value: 42, Unit: "°C")));

    [Fact]
    public void Rien_donne_un_statut()
        => Assert.Equal(CellKind.Status, CellViews.DeriveKind(Cell(), Reading.Empty));

    [Fact]
    public void Les_enfants_donnent_un_groupe_et_kind_explicite_l_emporte()
    {
        Assert.Equal(CellKind.Group, CellViews.DeriveKind(Cell(group: true), new Reading(Value: 1, Max: 2)));
        Assert.Equal(CellKind.Value, CellViews.DeriveKind(Cell(kind: "value"), new Reading(Value: 1, Max: 2)));
    }

    [Fact]
    public void Les_enfants_l_emportent_sur_kind_explicite()
        => Assert.Equal(CellKind.Group, CellViews.DeriveKind(new CellConfig { Id = "g", Kind = "value", Children = new List<string> { "a" } }, new Reading(Value: 1, Max: 2)));

    [Fact]
    public void Le_statut_de_la_source_l_emporte_sur_les_seuils()
        => Assert.Equal(Status.Busy, CellViews.DeriveStatus(Cell(), new Reading(Value: 99, Max: 100, Status: Status.Busy)));

    [Fact]
    public void Sans_statut_les_seuils_jugent_le_pourcentage()
    {
        Assert.Equal(Status.Crit, CellViews.DeriveStatus(Cell(), new Reading(Value: 80, Max: 100)));
        Assert.Equal(Status.Warn, CellViews.DeriveStatus(Cell(t: new Thresholds(Warn: 40)), new Reading(Value: 41, Unit: "°C")));
        Assert.Equal(Status.Off, CellViews.DeriveStatus(Cell(), Reading.Empty));
    }

    [Fact]
    public void La_legende_suit_le_type()
    {
        Assert.Equal("73%", CellViews.Caption(CellKind.Ring, new Reading(Value: 73.4, Max: 100)));
        Assert.Equal("42 °C", CellViews.Caption(CellKind.Value, new Reading(Value: 42, Unit: "°C")));
        Assert.Equal("1 240 €", CellViews.Caption(CellKind.Value, new Reading(Value: 1240, Unit: "€")));
        Assert.Equal("7", CellViews.Caption(CellKind.Value, new Reading(Value: 7)));
        Assert.Null(CellViews.Caption(CellKind.Status, Reading.Empty));
    }

    [Fact]
    public void Une_lecture_perimee_porte_son_age()
    {
        var view = CellViews.From(Cell(), new Reading(Value: 1, Max: 2).AsStale(1_000, "réseau"), nowMs: 1_000 + 5 * 60_000);
        Assert.True(view.Stale);
        Assert.Equal("il y a 5 min", view.StaleAge);
        Assert.Equal(0.5, view.Fraction);
    }

    [Fact]
    public void L_age_est_lisible()
    {
        Assert.Equal("à l'instant", CellViews.Age(30_000));
        Assert.Equal("il y a 2 h", CellViews.Age(2 * 3_600_000));
        Assert.Equal("il y a 3 j", CellViews.Age(3 * 86_400_000L));
    }
}

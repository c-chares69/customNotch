using CustomNotch.Core.Config;
using CustomNotch.Core.Model;
using CustomNotch.Core.Sources;
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
    public void Avec_un_maximum_l_anneau_l_emporte_sur_l_historique()
        => Assert.Equal(CellKind.Ring, CellViews.DeriveKind(Cell(), new Reading(Value: 30, Max: 100, History: new[] { (1L, 1.0), (2L, 3.0) })));

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
    public void Un_statut_avec_texte_a_une_legende_tronquee()
    {
        Assert.Equal("Bohemian R…", CellViews.Caption(CellKind.Status, new Reading(Text: "Bohemian Rhapsody")));
        Assert.Equal("Court", CellViews.Caption(CellKind.Status, new Reading(Text: "Court")));
        Assert.Null(CellViews.Caption(CellKind.Status, Reading.Empty));
        Assert.Null(CellViews.Caption(CellKind.Status, new Reading(Value: 3, Text: "x")));
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

    [Fact]
    public void L_image_de_la_lecture_arrive_dans_la_vue()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var view = CellViews.From(Cell(), new Reading(Text: "x", Image: bytes), 0);
        Assert.Same(bytes, view.Image);
    }

    [Fact]
    public void La_forme_et_l_activite_viennent_de_la_pilule_et_de_l_apparence()
    {
        var pill = new PillConfig { Id = "p", CellsShape = "square" };
        var app = new AppearanceConfig { Activity = "ring" };
        var cell = new CellConfig { Id = "c", Source = "system.cpu" };
        var v = CellViews.From(cell, new Reading(Value: 3, Max: 100), 0, pill, app);
        Assert.Equal("square", v.Shape);
        Assert.Equal("ring", v.Activity);
        Assert.True(v.ShowCaption);
    }

    [Fact]
    public void La_cellule_surcharge_l_activite_et_la_legende()
    {
        var cell = new CellConfig { Id = "c", Source = "system.cpu", Activity = "dot", Caption = false };
        var v = CellViews.From(cell, new Reading(Value: 3, Max: 100), 0, null, new AppearanceConfig { Activity = "ring" });
        Assert.Equal("dot", v.Activity);
        Assert.False(v.ShowCaption);
    }

    [Fact]
    public void Le_schema_de_la_source_fixe_la_legende_par_defaut()
    {
        var schema = new SourceSchema("media", "Média", Array.Empty<SchemaField>(), "music", "", "toggle", DefaultCaption: false);
        var v = CellViews.From(new CellConfig { Id = "m", Source = "media" }, new Reading(Text: "Titre"), 0, null, null, schema);
        Assert.False(v.ShowCaption);
        var forced = CellViews.From(new CellConfig { Id = "m", Source = "media", Caption = true }, new Reading(Text: "Titre"), 0, null, null, schema);
        Assert.True(forced.ShowCaption);
    }

    [Fact]
    public void Sans_options_les_defauts_sont_rond_pastille_legende()
    {
        var v = CellViews.From(new CellConfig { Id = "c" }, new Reading(Text: "x"), 0);
        Assert.Equal("round", v.Shape); Assert.Equal("dot", v.Activity); Assert.True(v.ShowCaption);
    }
}

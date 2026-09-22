using CustomNotch.App.Notch;
using CustomNotch.Core.Config;
using CustomNotch.Core.Model;
using Xunit;
namespace CustomNotch.App.Tests;

public class CardContentTests
{
    private static CellView View(string id, Reading r, string? label = null, bool stale = false)
        => CellViews.From(new CellConfig { Id = id, Label = label ?? id, Glyph = "cpu" }, stale ? r.AsStale(0, "réseau") : r, 60_000);

    [Fact]
    public void Une_cellule_simple_liste_ses_details_et_actions()
    {
        var reading = new Reading(Value: 73, Max: 100, Detail: new[] { new DetailRow("Session", "73 %", 0.73, "reset dans 51 min") }, Actions: new[] { new ActionSpec("refresh", "Rafraîchir") });
        var cell = new CellConfig { Id = "c", Label = "Claude", Actions = new CellActions { Card = new() { new ActionConfig { Label = "Ouvrir", Open = "https://claude.ai" } } } };
        var model = CardContent.Build(View("c", reading, "Claude"), cell, Array.Empty<CellView>());
        Assert.Equal("Claude", model.Title);
        Assert.Single(model.Rows);
        Assert.Equal(0.73, model.Rows[0].Fraction);
        Assert.Equal(Status.Warn, model.Rows[0].Tone);   // 73 % : seuils par défaut
        Assert.Equal(2, model.Actions.Count);
        Assert.Equal("Ouvrir", model.Actions[0].Label);
        Assert.Equal("refresh", model.Actions[1].SourceAction);
        Assert.Null(model.Note);
    }

    [Fact]
    public void Sans_detail_la_valeur_fait_une_ligne()
    {
        var model = CardContent.Build(View("t", new Reading(Value: 42, Unit: "°C"), "Temp"), new CellConfig { Id = "t" }, Array.Empty<CellView>());
        Assert.Single(model.Rows);
        Assert.Equal("42 °C", model.Rows[0].Text);
    }

    [Fact]
    public void Un_groupe_empile_ses_enfants()
    {
        var a = View("a", new Reading(Value: 10, Max: 100), "CPU");
        var b = View("b", new Reading(Value: 3, Unit: "Mo/s", History: new[] { (1L, 1.0), (2L, 3.0) }), "Réseau");
        var group = new CellConfig { Id = "g", Label = "Système", Children = new() { "a", "b" } };
        var model = CardContent.Build(CellViews.From(group, Reading.Empty, 0), group, new[] { a, b });
        Assert.Equal(2, model.Rows.Count);
        Assert.Equal("CPU", model.Rows[0].Label); Assert.Equal("10%", model.Rows[0].Text); Assert.Equal(0.1, model.Rows[0].Fraction);
        Assert.Equal("Réseau", model.Rows[1].Label); Assert.Equal("3 Mo/s", model.Rows[1].Text); Assert.Null(model.Rows[1].Fraction);
    }

    [Fact]
    public void Perime_ajoute_une_note_avec_l_age()
    {
        var model = CardContent.Build(View("c", new Reading(Value: 1, Max: 2), stale: true), new CellConfig { Id = "c" }, Array.Empty<CellView>());
        Assert.Contains("il y a 1 min", model.Note);
        Assert.Contains("réseau", model.Note);
    }
}

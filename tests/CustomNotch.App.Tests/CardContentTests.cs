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
        var bytes = new byte[] { 1, 2, 3 };
        var reading = new Reading(Value: 73, Max: 100, Detail: new[] { new DetailRow("Session", "73 %", 0.73, "reset dans 51 min") }, Actions: new[] { new ActionSpec("refresh", "Rafraîchir") }, Image: bytes);
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
        Assert.Same(bytes, model.Image);
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
        var b = View("b", new Reading(Value: 3, Unit: "Mo/s", History: new[] { (1L, 1.0), (2L, 3.0) }, Actions: new[] { new ActionSpec("toggle", "Basculer") }), "Réseau");
        var group = new CellConfig { Id = "g", Label = "Système", Children = new() { "a", "b" } };
        var model = CardContent.Build(CellViews.From(group, Reading.Empty, 0), group, new[] { a, b });
        Assert.Equal(2, model.Rows.Count);
        Assert.Equal("CPU", model.Rows[0].Label); Assert.Equal("10%", model.Rows[0].Text); Assert.Equal(0.1, model.Rows[0].Fraction);
        Assert.Equal("Réseau", model.Rows[1].Label); Assert.Equal("3 Mo/s", model.Rows[1].Text); Assert.Null(model.Rows[1].Fraction);
        // L'action d'un enfant de groupe vise ce enfant (TargetCellId), pas la cellule qui a ouvert la carte,
        // et l'id d'action lui-même n'est plus jamais découpé sur « : ».
        var action = Assert.Single(model.Actions);
        Assert.Equal("toggle", action.SourceAction);
        Assert.Equal("b", action.TargetCellId);
    }

    [Fact]
    public void Perime_ajoute_une_note_avec_l_age()
    {
        var model = CardContent.Build(View("c", new Reading(Value: 1, Max: 2), stale: true), new CellConfig { Id = "c" }, Array.Empty<CellView>());
        Assert.Contains("il y a 1 min", model.Note);
        Assert.Contains("réseau", model.Note);
    }

    [Fact]
    public void Un_groupe_a_une_synthese_et_des_valeurs_absolues()
    {
        var cpu = View("cpu", new Reading(Value: 34, Max: 100, Status: Status.Ok), "CPU");
        var mem = View("mem", new Reading(Value: 61, Max: 100, Status: Status.Ok, Detail: new[] { new DetailRow("Utilisée", "9,8 Go", 0.61) }), "Mémoire");
        var disk = View("disk", new Reading(Value: 80, Max: 100, Status: Status.Warn), "Disque");
        var group = new CellConfig { Id = "g", Label = "Système", Children = new() { "cpu", "mem", "disk" } };
        var model = CardContent.Build(CellViews.From(group, Reading.Empty, 0), group, new[] { cpu, mem, disk });
        Assert.Equal("Disque à surveiller", model.Subtitle);
        var cpuRow = model.Rows.Single(r => r.Label == "CPU");
        Assert.Equal("34%", cpuRow.Text);
        var memRow = model.Rows.Single(r => r.Label == "Mémoire");
        Assert.Equal("61% · 9,8 Go", memRow.Text);
    }

    [Fact]
    public void Un_enfant_critique_l_emporte_sur_un_enfant_a_surveiller()
    {
        var warn = View("w", new Reading(Value: 1, Status: Status.Warn), "Attention");
        var crit = View("c", new Reading(Value: 1, Status: Status.Crit), "Alerte");
        var ok = View("o", new Reading(Value: 1, Status: Status.Ok), "Calme");
        var group = new CellConfig { Id = "g", Children = new() { "w", "c", "o" } };
        var model = CardContent.Build(CellViews.From(group, Reading.Empty, 0), group, new[] { warn, crit, ok });
        Assert.Equal("Alerte critique", model.Subtitle);
    }

    [Fact]
    public void Un_groupe_sans_alerte_dit_tout_va_bien()
    {
        var a = View("a", new Reading(Value: 1, Status: Status.Ok), "A");
        var b = View("b", new Reading(Value: 1, Status: Status.Ok), "B");
        var group = new CellConfig { Id = "g", Children = new() { "a", "b" } };
        var model = CardContent.Build(CellViews.From(group, Reading.Empty, 0), group, new[] { a, b });
        Assert.Equal("tout va bien", model.Subtitle);
    }

    [Fact]
    public void Une_cellule_media_a_le_titre_en_tete()
    {
        var cover = new byte[] { 9, 9, 9 };
        var reading = new Reading(Text: "Bohemian Rhapsody — Queen", Status: Status.Busy,
            Detail: new[] { new DetailRow("position", "2:31 / 5:55", 0.42, "timeline:151000:355000:1790168101000") },
            Image: cover);
        var cell = new CellConfig { Id = "media", Source = "media", Label = "Spotify" };
        var view = CellViews.From(cell, reading, 0);
        var model = CardContent.Build(view, cell, Array.Empty<CellView>());
        Assert.Equal("Bohemian Rhapsody", model.Title);
        Assert.Equal("Queen", model.Subtitle);
        var row = Assert.Single(model.Rows);
        Assert.Equal("position", row.Label);
        Assert.Equal("2:31 / 5:55", row.Text);
        Assert.Same(cover, model.Image);
    }

    [Fact]
    public void Une_cellule_media_sans_lecture_garde_le_libelle_de_la_cellule()
    {
        var reading = new Reading(Text: "Aucune lecture", Status: Status.Off,
            Detail: new[] { new DetailRow("Lecture", "aucune — cliquer ouvre l'application") },
            Actions: new[] { new ActionSpec("open", "Ouvrir", "open") });
        var cell = new CellConfig { Id = "media", Source = "media", Label = "Spotify" };
        var view = CellViews.From(cell, reading, 0);
        var model = CardContent.Build(view, cell, Array.Empty<CellView>());
        Assert.Equal("Spotify", model.Title);
        Assert.Null(model.Subtitle);
        var row = Assert.Single(model.Rows);
        Assert.Equal("aucune — cliquer ouvre l'application", row.Text);
    }
}

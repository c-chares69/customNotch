using System.Text.Json.Nodes;
using Xunit;
using CustomNotch.Core.Model;
using CustomNotch.Core.Sources;
using CustomNotch.Core.Sources.Media;
namespace CustomNotch.Core.Tests.Sources;

public class MediaSourceTests
{
    private sealed class FakeSession : IMediaSession
    {
        public MediaState? State;
        public List<string> Calls = new();
        public event Action? Changed;
        public MediaState? Current() => State;
        public Task ToggleAsync() { Calls.Add("toggle"); return Task.CompletedTask; }
        public Task NextAsync() { Calls.Add("next"); return Task.CompletedTask; }
        public Task PreviousAsync() { Calls.Add("prev"); return Task.CompletedTask; }
        public void Fire() => Changed?.Invoke();
    }

    private static CellContext Ctx(string id = "m") => new(id, new JsonObject(), null);

    [Fact]
    public async Task En_lecture_la_cellule_est_occupee_avec_titre_et_actions()
    {
        var session = new FakeSession { State = new MediaState("Bohemian Rhapsody", "Queen", "Spotify", true) };
        var r = await new MediaSource(session).ReadAsync(Ctx(), CancellationToken.None);
        Assert.Equal("Bohemian Rhapsody — Queen", r.Text);
        Assert.Equal(Status.Busy, r.Status);
        Assert.Equal(new[] { "prev", "toggle", "next" }, r.Actions!.Select(a => a.Id));
    }

    [Fact]
    public async Task En_pause_le_statut_est_ok_et_le_bouton_dit_lecture()
    {
        var session = new FakeSession { State = new MediaState("Titre", "", "chrome", false) };
        var r = await new MediaSource(session).ReadAsync(Ctx(), CancellationToken.None);
        Assert.Equal("Titre", r.Text);
        Assert.Equal(Status.Ok, r.Status);
        Assert.Equal("play", r.Actions![1].Icon);
    }

    [Fact]
    public async Task Sans_session_ni_implementation_la_cellule_est_off()
    {
        var r1 = await new MediaSource(new FakeSession()).ReadAsync(Ctx(), CancellationToken.None);
        var r2 = await new MediaSource(null).ReadAsync(Ctx(), CancellationToken.None);
        Assert.Equal(Status.Off, r1.Status);
        Assert.Equal("Aucune lecture", r1.Text);
        Assert.Equal(Status.Off, r2.Status);
    }

    [Fact]
    public async Task La_position_fait_une_ligne_timeline()
    {
        var session = new FakeSession { State = new MediaState("t", "a", "app", true, PositionMs: 151_000, DurationMs: 355_000, PositionAtMs: 1_790_168_101_000) };
        var r = await new MediaSource(session).ReadAsync(Ctx(), CancellationToken.None);
        var row = Assert.Single(r.Detail!);
        Assert.Equal("position", row.Label);
        Assert.Equal("2:31 / 5:55", row.Text);
        Assert.Equal(0.425, row.Fraction!.Value, 3);
        Assert.StartsWith("timeline:", row.Hint);
        Assert.Equal("timeline:151000:355000:1790168101000", row.Hint);
    }

    [Fact]
    public async Task Sans_timeline_pas_de_ligne_position()
    {
        var session = new FakeSession { State = new MediaState("t", "a", "app", true) };
        var r = await new MediaSource(session).ReadAsync(Ctx(), CancellationToken.None);
        Assert.Empty(r.Detail!);
    }

    [Fact]
    public async Task Les_actions_media_n_ont_pas_de_libelle()
    {
        var session = new FakeSession { State = new MediaState("t", "a", "app", true) };
        var r = await new MediaSource(session).ReadAsync(Ctx(), CancellationToken.None);
        Assert.Equal(3, r.Actions!.Count);
        Assert.All(r.Actions, a => Assert.Equal("", a.Label));
        Assert.Equal(new[] { "prev", "pause", "next" }, r.Actions.Select(a => a.Icon));
    }

    [Fact]
    public async Task Sans_session_le_clic_ouvre_le_repli()
    {
        var opened = new List<string>();
        var src = new MediaSource(new FakeSession(), open: t => { opened.Add(t); return true; });
        var r = await src.ReadAsync(Ctx(), CancellationToken.None);
        var action = Assert.Single(r.Actions!);
        Assert.Equal("open", action.Id);
        await src.InvokeAsync("toggle", Ctx(), CancellationToken.None);
        await src.InvokeAsync("open", Ctx(), CancellationToken.None);
        Assert.Equal(new[] { "spotify:", "spotify:" }, opened);
    }

    [Fact]
    public async Task Le_repli_se_lit_dans_les_params_de_la_cellule()
    {
        var opened = new List<string>();
        var src = new MediaSource(new FakeSession(), open: t => { opened.Add(t); return true; });
        var ps = new JsonObject { ["fallbackOpen"] = "https://open.spotify.com" };
        await src.InvokeAsync("toggle", new CellContext("m", ps, null), CancellationToken.None);
        Assert.Equal(new[] { "https://open.spotify.com" }, opened);
    }

    [Fact]
    public async Task Les_actions_atteignent_la_session()
    {
        var session = new FakeSession { State = new MediaState("t", "a", "app", true) };
        var src = new MediaSource(session);
        await src.InvokeAsync("toggle", Ctx(), CancellationToken.None);
        await src.InvokeAsync("next", Ctx(), CancellationToken.None);
        await src.InvokeAsync("prev", Ctx(), CancellationToken.None);
        Assert.Equal(new[] { "toggle", "next", "prev" }, session.Calls);
    }

    [Fact]
    public async Task Un_changement_de_session_pousse_les_cellules_lues()
    {
        var session = new FakeSession();
        var src = new MediaSource(session);
        var pushed = new List<string>();
        src.Pushed += pushed.Add;
        await src.ReadAsync(Ctx("a"), CancellationToken.None);
        await src.ReadAsync(Ctx("b"), CancellationToken.None);
        session.Fire();
        Assert.Equal(new[] { "a", "b" }, pushed.OrderBy(x => x));
    }

    [Fact]
    public void Le_schema_declare_le_clic_par_defaut_et_le_registre_l_enregistre()
    {
        var registry = CoreSources.Build(new FakeSession());
        var schema = registry.Get("media")!.Schema;
        Assert.Equal("toggle", schema.DefaultAction);
        Assert.Equal("music", schema.DefaultGlyph);
        Assert.Contains("media", CoreSources.Build().Types);
    }

    [Fact]
    public async Task La_pochette_traverse_la_lecture()
    {
        var session = new FakeSession { State = new MediaState("t", "a", "app", true, new byte[] { 1, 2, 3 }) };
        var r = await new MediaSource(session).ReadAsync(Ctx(), CancellationToken.None);
        Assert.Equal(new byte[] { 1, 2, 3 }, r.Image);

        var sansPochette = new FakeSession { State = new MediaState("t", "a", "app", true) };
        var r2 = await new MediaSource(sansPochette).ReadAsync(Ctx(), CancellationToken.None);
        Assert.Null(r2.Image);
    }

    [Fact]
    public void Deux_etats_a_pochette_identique_sont_egaux()
    {
        var a = new MediaState("t", "a", "app", true, new byte[] { 1, 2, 3 });
        var b = new MediaState("t", "a", "app", true, new byte[] { 1, 2, 3 });
        Assert.Equal(a, b);

        var c = new MediaState("t", "a", "app", true, new byte[] { 1, 2, 4 });
        Assert.NotEqual(a, c);
    }
}

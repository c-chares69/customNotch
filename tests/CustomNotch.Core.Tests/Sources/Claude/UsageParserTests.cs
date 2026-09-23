using System.Text.Json.Nodes;
using CustomNotch.Core.Sources.Claude;
using Xunit;
namespace CustomNotch.Core.Tests.Sources.Claude;

public class UsageParserTests
{
    private static JsonNode Fixture() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Sources", "Claude", "usage-2026-09-23.json")))!;

    [Fact]
    public void La_reponse_reelle_rend_trois_fenetres_dedoublonnees_et_le_breakdown()
    {
        var (windows, breakdown) = UsageParser.Parse(Fixture());

        Assert.Equal(3, windows.Count);
        Assert.Equal(("session", "Session (5 h)", 3.0), (windows[0].Id, windows[0].Label, windows[0].Percent));
        Assert.Equal(("weekly_all", "Semaine (tous modèles)", 36.0), (windows[1].Id, windows[1].Label, windows[1].Percent));
        Assert.Equal(("weekly_scoped", "Semaine (Fable)", 29.0), (windows[2].Id, windows[2].Label, windows[2].Percent));
        Assert.All(windows, w => Assert.NotNull(w.ResetsAtMs));

        Assert.Equal(4, breakdown.Count);
        Assert.Equal(new[] { "Claude Code", "Chats", "Cowork", "Other" }, breakdown.Select(b => b.Label));
        Assert.Equal(94.0, breakdown[0].Percent);

        var headline = UsageParser.Headline(windows);
        Assert.Equal("session", headline!.Id);
    }

    [Fact]
    public void Sans_limits_le_repli_five_hour_seven_day_rend_deux_fenetres()
    {
        var root = JsonNode.Parse("""
            {"five_hour":{"utilization":10,"resets_at":"2026-09-23T16:00:00+00:00"},
             "seven_day":{"utilization":20,"resets_at":"2026-09-26T10:00:00+00:00"},
             "limits":[]}
            """)!;

        var (windows, _) = UsageParser.Parse(root);

        Assert.Equal(new[] { "session", "weekly_all" }, windows.Select(w => w.Id));
        Assert.Equal(10.0, windows[0].Percent);
        Assert.Equal(20.0, windows[1].Percent);
    }

    [Fact]
    public void Session_et_five_hour_au_meme_reset_fusionnent_en_une_fenetre()
    {
        var root = JsonNode.Parse("""
            {"five_hour":{"utilization":10,"resets_at":"2026-09-23T16:00:00+00:00"},
             "limits":[{"kind":"session","percent":10,"resets_at":"2026-09-23T16:00:00+00:00"}]}
            """)!;

        var (windows, _) = UsageParser.Parse(root);

        Assert.Single(windows);
        Assert.Equal("session", windows[0].Id);
    }

    [Fact]
    public void Une_reponse_sans_rien_ne_rend_aucune_fenetre()
    {
        var (windows, breakdown) = UsageParser.Parse(JsonNode.Parse("{}")!);

        Assert.Empty(windows);
        Assert.Empty(breakdown);
        Assert.Null(UsageParser.Headline(windows));
    }

    [Fact]
    public void Le_libelle_d_un_kind_inconnu_est_rendu_lisible()
    {
        Assert.Equal("Autre chose", UsageParser.Label("autre_chose", null));
    }
}

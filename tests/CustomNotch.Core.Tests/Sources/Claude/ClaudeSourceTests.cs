using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using CustomNotch.Core.Model;
using CustomNotch.Core.Sources;
using CustomNotch.Core.Sources.Claude;
using Xunit;
namespace CustomNotch.Core.Tests.Sources.Claude;

public class ClaudeSourceTests : IDisposable
{
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(respond(request));
    }

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "cn-claude-src-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string _sessionsDir;
    private readonly string _backoffPath;
    private readonly Dictionary<int, long> _live = new();
    private long _clock = 1_700_000_000_000;
    private ClaudeCredentials? _credentials;
    private SessionRegistry? _sessions;
    private TokenRenewal? _renewal;

    public ClaudeSourceTests()
    {
        Directory.CreateDirectory(_dir);
        _sessionsDir = Path.Combine(_dir, "sessions");
        Directory.CreateDirectory(_sessionsDir);
        _backoffPath = Path.Combine(_dir, "claude-backoff.json");
        _credentials = new ClaudeCredentials("token-abc", _clock + 60 * 60_000, "pro", "default");
    }

    public void Dispose()
    {
        _sessions?.Dispose();
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    private static string Fixture() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Sources", "Claude", "usage-2026-09-23.json"));

    private static HttpResponseMessage Ok() => new(HttpStatusCode.OK)
    { Content = new StringContent(Fixture(), Encoding.UTF8, "application/json") };

    private ClaudeSource Build(Func<HttpRequestMessage, HttpResponseMessage>? respond = null, Func<string, Task<int>>? runHidden = null)
    {
        var http = new HttpClient(new FakeHandler(respond ?? (_ => Ok())));
        var usage = new UsageClient(http, () => _clock);
        _renewal = new TokenRenewal(() => _credentials, () => "claude", runHidden ?? (_ => Task.FromResult(0)), () => _clock);
        _sessions = new SessionRegistry(_sessionsDir, pid => _live.TryGetValue(pid, out var v) ? v : (long?)null, () => _clock);
        return new ClaudeSource(_ => _credentials, usage, _renewal, _sessions, _backoffPath, () => _clock);
    }

    private void WriteSession(int pid, string status, long procStart, string? name = null)
    {
        _live[pid] = procStart;
        var o = new JsonObject { ["pid"] = pid, ["cwd"] = @"C:\proj\foo", ["status"] = status, ["procStart"] = procStart.ToString() };
        if (name is not null) o["name"] = name;
        File.WriteAllText(Path.Combine(_sessionsDir, $"{pid}.json"), o.ToJsonString());
    }

    private static CellContext Ctx(string cellId = "claude", JsonObject? @params = null) => new(cellId, @params ?? new JsonObject(), null);

    [Fact]
    public async Task Jeton_valide_donne_la_valeur_les_fenetres_et_la_repartition()
    {
        var source = Build();

        var reading = await source.ReadAsync(Ctx(), CancellationToken.None);

        Assert.Equal(3, reading.Value);
        Assert.Equal(100, reading.Max);
        Assert.Null(reading.Status);
        Assert.Equal(3, reading.Detail!.Count(d => !d.Label.StartsWith('·')));
        Assert.Contains(reading.Detail!, d => d.Label == "· Claude Code" && d.Text == "94 %");
    }

    [Fact]
    public async Task Une_session_waiting_donne_le_statut_Attention()
    {
        var source = Build();
        WriteSession(500, "waiting", _clock, name: "customNotch");
        _sessions!.Scan();

        var reading = await source.ReadAsync(Ctx(), CancellationToken.None);

        Assert.Equal(Status.Attention, reading.Status);
        Assert.Contains(reading.Detail!, d => d.Label == "customNotch" && d.Text == "en attente de toi" && d.Tone == Status.Attention);
    }

    [Fact]
    public async Task Une_session_busy_donne_le_statut_Busy()
    {
        var source = Build();
        WriteSession(501, "busy", _clock, name: "customNotch");
        _sessions!.Scan();

        var reading = await source.ReadAsync(Ctx(), CancellationToken.None);

        Assert.Equal(Status.Busy, reading.Status);
        Assert.Contains(reading.Detail!, d => d.Label == "customNotch" && d.Tone == Status.Busy);
    }

    [Fact]
    public async Task Sans_jeton_le_statut_est_Off_avec_l_action_sign_in()
    {
        _credentials = null;
        var source = Build();

        var reading = await source.ReadAsync(Ctx(), CancellationToken.None);

        Assert.Equal(Status.Off, reading.Status);
        Assert.Equal("Connexion requise", reading.Text);
        Assert.Contains(reading.Actions!, a => a.Id == "sign-in");
    }

    [Fact]
    public async Task Jeton_expire_et_renouvellement_echoue_garde_la_derniere_valeur_perimee()
    {
        var source = Build();
        var fresh = await source.ReadAsync(Ctx(), CancellationToken.None);
        Assert.Equal(3, fresh.Value);

        // Le renouvellement (runHidden par défaut) ne change rien à _credentials : TryRenewAsync échoue.
        _credentials = _credentials! with { ExpiresAtMs = _clock - 1_000 };

        var stale = await source.ReadAsync(Ctx(), CancellationToken.None);

        Assert.Equal(3, stale.Value);
        Assert.NotNull(stale.StaleSinceMs);
        Assert.Contains("expiré", stale.Error);
    }

    [Fact]
    public async Task Un_429_ecrit_le_backoff_et_rend_une_lecture_perimee()
    {
        var source = Build(respond: _ =>
        {
            var response = new HttpResponseMessage((HttpStatusCode)429) { Content = new StringContent("") };
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
            return response;
        });

        var reading = await source.ReadAsync(Ctx(), CancellationToken.None);

        Assert.NotNull(reading.StaleSinceMs);
        Assert.Contains("nouvel essai", reading.Error);
        Assert.NotNull(Backoff.Load(_backoffPath));
    }

    [Fact]
    public async Task Refresh_pousse_la_cellule()
    {
        var source = Build();
        await source.ReadAsync(Ctx(), CancellationToken.None);
        string? pushed = null;
        source.Pushed += id => pushed = id;

        await source.InvokeAsync("refresh", Ctx(), CancellationToken.None);

        Assert.Equal("claude", pushed);
    }

    [Fact]
    public async Task Un_changement_de_session_pousse_les_cellules_claude_connues()
    {
        var source = Build();
        await source.ReadAsync(Ctx(), CancellationToken.None);
        string? pushed = null;
        source.Pushed += id => pushed = id;

        WriteSession(600, "busy", _clock);
        _sessions!.Scan();

        Assert.Equal("claude", pushed);
    }

    [Fact]
    public async Task Les_pids_lances_par_le_renouvellement_sont_ignores_du_registre()
    {
        var source = Build();
        await source.ReadAsync(Ctx(), CancellationToken.None); // amorce _renewal côté ClaudeSource
        _live[700] = _clock;
        WriteSession(700, "busy", _clock);
        _renewal!.NoteLaunched(700);

        Assert.Empty(_sessions!.Scan());
    }
}

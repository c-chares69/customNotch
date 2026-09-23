using System.Text.Json.Nodes;
using CustomNotch.Core.Sources.Claude;
using Xunit;
namespace CustomNotch.Core.Tests.Sources.Claude;

public class SessionRegistryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "cn-sessions-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly Dictionary<int, long> _live = new();
    private long _clock = 1_700_000_000_000;
    private readonly SessionRegistry _registry;

    public SessionRegistryTests()
    {
        Directory.CreateDirectory(_dir);
        _registry = new SessionRegistry(_dir, pid => _live.TryGetValue(pid, out var v) ? v : (long?)null, () => _clock);
    }

    public void Dispose()
    {
        _registry.Dispose();
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    private void WriteSession(int pid, string status, string? sessionId = null, long? startedAt = null,
        long? updatedAt = null, long? procStart = null, string? name = null, string cwd = @"C:\proj\foo",
        string? entrypoint = null)
    {
        var o = new JsonObject { ["pid"] = pid, ["cwd"] = cwd, ["status"] = status };
        if (name is not null) o["name"] = name;
        if (sessionId is not null) o["sessionId"] = sessionId;
        if (startedAt is not null) o["startedAt"] = startedAt.Value;
        if (updatedAt is not null) o["updatedAt"] = updatedAt.Value;
        if (procStart is not null) o["procStart"] = procStart.Value.ToString();
        if (entrypoint is not null) o["entrypoint"] = entrypoint;
        File.WriteAllText(Path.Combine(_dir, $"{pid}.json"), o.ToJsonString());
    }

    [Fact]
    public void Un_fichier_vivant_busy_donne_une_session_Busy_nommee()
    {
        _live[100] = 1_000_000_000_000_000L;
        WriteSession(100, "busy", procStart: _live[100], name: "foo");

        var sessions = _registry.Scan();

        var s = Assert.Single(sessions);
        Assert.Equal(SessionState.Busy, s.State);
        Assert.Equal("foo", s.Name);
        Assert.Equal(100, s.Pid);
    }

    [Fact]
    public void Un_fichier_waiting_donne_une_session_Waiting()
    {
        _live[101] = 1_000_000_000_000_000L;
        WriteSession(101, "waiting", procStart: _live[101]);

        var s = Assert.Single(_registry.Scan());
        Assert.Equal(SessionState.Waiting, s.State);
    }

    [Fact]
    public void Fichier_dont_le_pid_n_existe_pas_est_ignore()
    {
        WriteSession(102, "busy", procStart: 1_000_000_000_000_000L);
        // pid 102 absent de _live : le processus n'existe plus.

        Assert.Empty(_registry.Scan());
    }

    [Fact]
    public void Pid_recycle_par_un_autre_processus_est_ignore()
    {
        const long fileTime = 5_000_000_000_000_000L;
        WriteSession(103, "busy", procStart: fileTime);
        // Le pid existe mais son heure de démarrage réelle diffère d'une heure (36e9 unités FILETIME) :
        // c'est un autre processus qui a hérité du même pid, pas la session Claude Code du fichier.
        _live[103] = fileTime + 36_000_000_000L;

        Assert.Empty(_registry.Scan());
    }

    [Fact]
    public void Sans_procStart_la_vivacite_se_fie_a_startedAt()
    {
        const long fileTime = 1_000_000_000_000_000L;
        var startedAtMs = DateTimeOffset.FromFileTime(fileTime).ToUnixTimeMilliseconds();
        _live[104] = fileTime;
        WriteSession(104, "waiting", startedAt: startedAtMs);

        var s = Assert.Single(_registry.Scan());
        Assert.Equal(SessionState.Waiting, s.State);
    }

    [Fact]
    public void Sans_procStart_un_startedAt_trop_eloigne_est_ignore()
    {
        const long fileTime = 1_000_000_000_000_000L;
        var startedAtMs = DateTimeOffset.FromFileTime(fileTime).ToUnixTimeMilliseconds() + 5_000;
        _live[105] = fileTime;
        WriteSession(105, "busy", startedAt: startedAtMs);

        Assert.Empty(_registry.Scan());
    }

    [Fact]
    public void Deux_fichiers_meme_sessionId_startedAt_differents_garde_le_plus_recent()
    {
        _live[106] = 1_000_000_000_000_000L;
        _live[107] = 2_000_000_000_000_000L;
        WriteSession(106, "idle", sessionId: "s1", startedAt: 1_000, procStart: _live[106], name: "ancienne");
        WriteSession(107, "idle", sessionId: "s1", startedAt: 2_000, procStart: _live[107], name: "récente");

        var s = Assert.Single(_registry.Scan());
        Assert.Equal("récente", s.Name);
        Assert.Equal(107, s.Pid);
    }

    [Fact]
    public void Pid_ignore_est_absent_du_resultat()
    {
        _live[108] = 1_000_000_000_000_000L;
        WriteSession(108, "busy", procStart: _live[108]);
        _registry.IgnoredPids = () => new[] { 108 };

        Assert.Empty(_registry.Scan());
    }

    [Fact]
    public void Session_disparue_reste_Ended_dix_minutes_puis_disparait()
    {
        _live[109] = 1_000_000_000_000_000L;
        WriteSession(109, "busy", procStart: _live[109], name: "partie");
        Assert.Single(_registry.Scan());

        File.Delete(Path.Combine(_dir, "109.json"));
        _clock += 60_000; // 1 min plus tard
        var second = _registry.Scan();
        var ended = Assert.Single(second);
        Assert.Equal(SessionState.Ended, ended.State);
        Assert.Equal(_clock, ended.SinceMs);
        Assert.Equal("partie", ended.Name);

        _clock += 11 * 60_000; // 11 min après la disparition
        Assert.Empty(_registry.Scan());
    }

    [Fact]
    public void Changed_leve_seulement_quand_le_resultat_differe()
    {
        var fired = 0;
        _registry.Changed += () => fired++;

        _live[110] = 1_000_000_000_000_000L;
        WriteSession(110, "busy", procStart: _live[110]);
        _registry.Scan();
        Assert.Equal(1, fired);

        _registry.Scan(); // rien n'a changé
        Assert.Equal(1, fired);

        File.Delete(Path.Combine(_dir, "110.json"));
        _registry.Scan();
        Assert.Equal(2, fired);
    }

    [Fact]
    public void Parse_tolere_un_json_sans_procStart_ni_sessionId()
    {
        var node = JsonNode.Parse("""{"pid":200,"cwd":"C:\\x\\bar","status":"waiting"}""")!;

        var record = SessionRecord.Parse(node);

        Assert.NotNull(record);
        Assert.Equal(200, record!.Pid);
        Assert.Null(record.SessionId);
        Assert.Null(record.ProcStartFileTime);
        Assert.Equal("bar", record.Name);
    }

    [Fact]
    public void Parse_rend_null_sans_pid_ou_sans_cwd()
    {
        Assert.Null(SessionRecord.Parse(JsonNode.Parse("""{"cwd":"C:\\x"}""")!));
        Assert.Null(SessionRecord.Parse(JsonNode.Parse("""{"pid":1}""")!));
    }

    [Fact]
    public void Surface_depuis_entrypoint()
    {
        _live[111] = 1_000_000_000_000_000L;
        _live[112] = 2_000_000_000_000_000L;
        _live[113] = 3_000_000_000_000_000L;
        WriteSession(111, "idle", procStart: _live[111], entrypoint: "claude-vscode");
        WriteSession(112, "idle", procStart: _live[112], entrypoint: "claude-desktop-win");
        WriteSession(113, "idle", procStart: _live[113], entrypoint: "local-agent");

        var byPid = _registry.Scan().ToDictionary(s => s.Pid);
        Assert.Equal("VS Code", byPid[111].Surface);
        Assert.Equal("Desktop", byPid[112].Surface);
        Assert.Equal("Agent", byPid[113].Surface);
    }

    [Fact]
    public void Sans_dossier_le_scan_rend_une_liste_vide_sans_exception()
    {
        Directory.Delete(_dir, true);

        Assert.Empty(_registry.Scan());
    }
}

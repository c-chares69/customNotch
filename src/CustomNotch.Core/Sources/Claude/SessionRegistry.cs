using System.Text.Json;
using System.Text.Json.Nodes;

namespace CustomNotch.Core.Sources.Claude;

/// <summary>Le registre que Claude Code tient lui-même dans <c>~/.claude/sessions/&lt;pid&gt;.json</c>, un
/// fichier par processus CLI en cours - lecture seule, jamais écrit. Une session est vivante quand son pid
/// existe encore et que l'heure de démarrage du processus vivant correspond à celle du fichier (le pid n'a pas
/// été recyclé par un autre programme entre-temps) ; dédoublonnée par <c>sessionId</c> ; une session disparue
/// reste visible 10 min (<see cref="EndedKeepMs"/>), « terminée il y a 3 min », plutôt que de sauter d'un coup.
/// Horloge, dossier et vivacité des processus sont injectés - testable sans processus réel.</summary>
public sealed class SessionRegistry : IDisposable
{
    public const long EndedKeepMs = 10 * 60_000;
    /// <summary>Tolérance de rapprochement des heures de démarrage, en unités FILETIME (100 ns) : 2 s.</summary>
    private const long FileTimeToleranceUnits = 20_000_000;
    private const long StartedAtToleranceMs = 2_000;
    private const int DebounceMs = 120;
    private const int TickMs = 2_000;

    private readonly string _dir;
    private readonly Func<int, long?> _processStartFileTime;
    private readonly Func<long> _now;
    private readonly object _lock = new();
    private readonly HashSet<string> _loggedUnreadable = new();

    private Dictionary<string, ClaudeSession> _lastLive = new();
    private Dictionary<string, ClaudeSession> _ended = new();
    private IReadOnlyList<ClaudeSession> _current = Array.Empty<ClaudeSession>();

    private FileSystemWatcher? _watcher;
    private Timer? _debounce;
    private Timer? _tick;
    private bool _disposed;

    public SessionRegistry(string dir, Func<int, long?> processStartFileTime, Func<long> now)
    {
        _dir = dir;
        _processStartFileTime = processStartFileTime;
        _now = now;
    }

    /// <summary>Les pids que ce registre doit ignorer - typiquement ceux qu'un <c>TokenRenewal</c> vient de
    /// lancer pour renouveler le jeton (<c>claude -p</c>) : ce ne sont pas des sessions de travail à montrer.</summary>
    public Func<IReadOnlyCollection<int>> IgnoredPids { get; set; } = () => Array.Empty<int>();

    public event Action? Changed;

    public IReadOnlyList<ClaudeSession> Current
    {
        get { lock (_lock) return _current; }
    }

    /// <summary>Synchrone, sans exception : un fichier illisible ou malformé est sauté (journalisé au plus une
    /// fois par nom de fichier), un dossier absent (Claude Code jamais lancé) rend juste une liste vide. Lève
    /// <see cref="Changed"/> hors du verrou, seulement quand le résultat diffère du précédent.</summary>
    public IReadOnlyList<ClaudeSession> Scan()
    {
        bool changed;
        IReadOnlyList<ClaudeSession> result;
        try
        {
            lock (_lock)
            {
                if (_disposed) return _current;
                result = ScanCore(_now());
                changed = !result.SequenceEqual(_current);
                _current = result;
            }
        }
        catch (Exception ex)
        {
            // Frontière : rien de ce qui peut arriver en lisant un dossier partagé (accès concurrent, disque
            // qui décroche) ne doit remonter jusqu'au thread du watcher, du minuteur ou d'un appelant naïf.
            Log.Warning("claude", $"lecture du registre des sessions : {ex.Message}");
            lock (_lock) return _current;
        }
        if (changed) Changed?.Invoke();
        return result;
    }

    /// <summary>Appelé sous <c>_lock</c> : construit la liste vivante + terminées récentes, et tient à jour
    /// <c>_lastLive</c>/<c>_ended</c> pour le prochain appel (c'est ici que la fenêtre de 10 min vit).</summary>
    private List<ClaudeSession> ScanCore(long nowMs)
    {
        var ignored = SafeIgnoredPids();
        var alive = AliveRecords(ignored);

        // Une session vivante au dernier scan et absente de celui-ci vient de disparaître : elle entre dans
        // `_ended` avec l'instant présent comme heure de disparition, une seule fois (un scan suivant qui la
        // trouve encore absente ne doit pas repousser son horloge de péremption).
        foreach (var (id, previous) in _lastLive)
            if (!alive.ContainsKey(id) && !_ended.ContainsKey(id))
                _ended[id] = previous with { State = SessionState.Ended, SinceMs = nowMs };

        // Redevenue vivante (même sessionId, processus relancé) : elle sort de `_ended`.
        foreach (var id in alive.Keys) _ended.Remove(id);

        foreach (var id in _ended.Where(kv => nowMs - kv.Value.SinceMs >= EndedKeepMs).Select(kv => kv.Key).ToList())
            _ended.Remove(id);

        var liveSessions = new Dictionary<string, ClaudeSession>();
        foreach (var (id, record) in alive)
            liveSessions[id] = ToSession(id, record, nowMs);
        _lastLive = liveSessions;

        return liveSessions.Values.Concat(_ended.Values)
            .OrderBy(s => s.State == SessionState.Ended ? 1 : 0)
            .ThenBy(s => s.Name, StringComparer.Ordinal)
            .ThenBy(s => s.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Les fichiers vivants, dédoublonnés par <c>sessionId</c> (le pid sert de repli quand le fichier
    /// n'en donne pas) en gardant le plus récent (<c>startedAt</c> le plus grand).</summary>
    private Dictionary<string, SessionRecord> AliveRecords(IReadOnlyCollection<int> ignored)
    {
        var result = new Dictionary<string, SessionRecord>();
        string[] files;
        try
        {
            files = Directory.Exists(_dir) ? Directory.GetFiles(_dir, "*.json") : Array.Empty<string>();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning("claude", $"dossier des sessions illisible : {ex.Message}");
            return result;
        }

        foreach (var file in files)
        {
            var record = TryRead(file);
            if (record is null) continue;
            if (ignored.Contains(record.Pid)) continue;
            if (!IsAlive(record)) continue;

            var id = record.SessionId is { Length: > 0 } sid ? sid : $"pid-{record.Pid}";
            if (result.TryGetValue(id, out var existing) &&
                (existing.StartedAtMs ?? long.MinValue) >= (record.StartedAtMs ?? long.MinValue))
                continue;
            result[id] = record;
        }
        return result;
    }

    private SessionRecord? TryRead(string file)
    {
        try
        {
            var text = File.ReadAllText(file);
            var node = JsonNode.Parse(text);
            return node is null ? null : SessionRecord.Parse(node);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            if (_loggedUnreadable.Add(Path.GetFileName(file)))
                Log.Warning("claude", $"session illisible, ignorée : {Path.GetFileName(file)} ({ex.Message})");
            return null;
        }
    }

    private IReadOnlyCollection<int> SafeIgnoredPids()
    {
        try { return IgnoredPids() ?? Array.Empty<int>(); }
        catch (Exception ex)
        {
            Log.Warning("claude", $"pids ignorés : {ex.Message}");
            return Array.Empty<int>();
        }
    }

    /// <summary>Vivante = le pid existe encore (<paramref name="processStartFileTime"/> via le délégué) et son
    /// heure de démarrage correspond à celle du fichier - sinon le pid a été recyclé par un autre processus.
    /// Repli sur <c>startedAt</c> (millisecondes) si le fichier n'a pas de <c>procStart</c> ; si ni l'un ni
    /// l'autre n'est connu, l'existence du pid suffit.</summary>
    private bool IsAlive(SessionRecord record)
    {
        var liveFileTime = _processStartFileTime(record.Pid);
        if (liveFileTime is null) return false;

        if (record.ProcStartFileTime is { } wantFileTime)
            return Math.Abs(liveFileTime.Value - wantFileTime) <= FileTimeToleranceUnits;

        if (record.StartedAtMs is { } startedAtMs)
        {
            var liveMs = DateTimeOffset.FromFileTime(liveFileTime.Value).ToUnixTimeMilliseconds();
            return Math.Abs(liveMs - startedAtMs) <= StartedAtToleranceMs;
        }

        return true;
    }

    private static ClaudeSession ToSession(string id, SessionRecord r, long nowMs) =>
        new(id, r.Name, StateOf(r.RawStatus), r.UpdatedAtMs ?? r.StartedAtMs ?? nowMs, SurfaceOf(r.Entrypoint), r.Pid);

    private static SessionState StateOf(string rawStatus) => rawStatus switch
    {
        "waiting" => SessionState.Waiting,
        "busy" => SessionState.Busy,
        _ => SessionState.Idle,
    };

    private static string SurfaceOf(string? entrypoint) => entrypoint switch
    {
        "claude-vscode" => "VS Code",
        { } e when e.StartsWith("claude-desktop", StringComparison.Ordinal) => "Desktop",
        "local-agent" => "Agent",
        _ => "Terminal",
    };

    /// <summary>Démarre la surveillance : un <see cref="FileSystemWatcher"/> (rebond 120 ms, comme
    /// <c>ConfigStore</c>) pour réagir vite, et un tic de 2 s qui appelle <see cref="Scan"/> quoi qu'il arrive.
    /// Le tic reste nécessaire malgré le watcher pour deux raisons que le watcher ne couvre pas : un processus
    /// qui meurt sans jamais toucher au dossier (rien à observer), et Claude Code qui réécrit <c>status</c> dans
    /// le même fichier sans que Windows livre un événement fiable à chaque fois. Le tic sert aussi à retenter la
    /// création du watcher si le dossier n'existait pas encore au démarrage (Claude Code jamais lancé sur ce
    /// poste) - le créer n'est pas notre rôle, seulement attendre qu'il apparaisse.</summary>
    public void Start()
    {
        lock (_lock)
        {
            if (_disposed) return;
            TryEnsureWatcher();
            _tick ??= new Timer(OnTick, null, TickMs, TickMs);
        }
    }

    private void TryEnsureWatcher()
    {
        if (_watcher is not null) return;
        try
        {
            if (!Directory.Exists(_dir)) return;
            var w = new FileSystemWatcher(_dir, "*.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            };
            w.Created += (_, _) => Bump();
            w.Deleted += (_, _) => Bump();
            w.Changed += (_, _) => Bump();
            w.Renamed += (_, _) => Bump();
            w.EnableRaisingEvents = true;
            _watcher = w;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Log.Warning("claude", $"surveillance du registre des sessions : {ex.Message}");
        }
    }

    private void Bump()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _debounce ??= new Timer(OnDebounce, null, Timeout.Infinite, Timeout.Infinite);
            _debounce.Change(DebounceMs, Timeout.Infinite);
        }
    }

    private void OnDebounce(object? state) => SafeScanOnTimerThread();

    private void OnTick(object? state)
    {
        lock (_lock)
        {
            if (_disposed) return;
            TryEnsureWatcher();
        }
        SafeScanOnTimerThread();
    }

    /// <summary>Le watcher et le minuteur tournent sur des threads du pool : une exception qui s'en échappe n'a
    /// personne pour l'attraper et tue le processus. <see cref="Scan"/> se protège déjà elle-même, ce filet reste
    /// pour tout ce qui pourrait s'ajouter autour plus tard.</summary>
    private void SafeScanOnTimerThread()
    {
        lock (_lock) { if (_disposed) return; }
        try { Scan(); }
        catch (Exception ex) { Log.Error("claude", $"registre des sessions (minuterie) : {ex.Message}"); }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
            _watcher?.Dispose();
            _watcher = null;
            _debounce?.Dispose();
            _debounce = null;
            _tick?.Dispose();
            _tick = null;
        }
    }
}

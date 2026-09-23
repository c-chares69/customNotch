using System.Globalization;
using CustomNotch.Core.Model;
using CoreStatus = CustomNotch.Core.Model.Status;

namespace CustomNotch.Core.Sources.Claude;

/// <summary>La cellule <c>claude</c> : assemble le jeton, l'usage (<see cref="UsageClient"/>) et les sessions
/// (<see cref="SessionRegistry"/>) en une seule <see cref="Reading"/> - jamais un chiffre inventé. Une source
/// qui échoue rend la dernière lecture connue, marquée périmée avec la raison (<see cref="Reading.AsStale"/>),
/// jamais une cellule vide. Une seule instance sert toutes les cellules <c>claude</c> (comme
/// <see cref="Media.MediaSource"/>) : elle retient leurs ids pour les pousser quand une session change d'état.</summary>
public sealed class ClaudeSource : SourceBase
{
    private readonly Func<string, ClaudeCredentials?> _readCredentials;
    private readonly UsageClient _usage;
    private readonly TokenRenewal _renewal;
    private readonly SessionRegistry _sessions;
    private readonly string _backoffPath;
    private readonly Func<long> _now;

    private readonly object _lock = new();
    private readonly HashSet<string> _cells = new();
    private readonly Dictionary<string, Reading> _last = new();
    private long? _backoffUntilMs;
    private int _failures;
    private long? _lastRenewalMs;
    private ClaudeStatus _status = new(null, null, null, null, null, Array.Empty<ClaudeSession>(), null);

    public ClaudeSource(Func<string, ClaudeCredentials?> readCredentials, UsageClient usage, TokenRenewal renewal,
        SessionRegistry sessions, string backoffPath, Func<long> now)
    {
        _readCredentials = readCredentials;
        _usage = usage;
        _renewal = renewal;
        _sessions = sessions;
        _backoffPath = backoffPath;
        _now = now;
        _backoffUntilMs = Backoff.Load(backoffPath);

        // Les pids qu'un renouvellement vient de lancer (`claude -p`) ne sont pas des sessions de travail :
        // le registre ne doit pas les montrer dans la carte.
        sessions.IgnoredPids = () => renewal.LaunchedPids;
        // Une session qui change d'état (busy → waiting, terminée…) doit se voir tout de suite, hors cadence -
        // même patron que MediaSource : pousser toutes les cellules claude connues.
        sessions.Changed += RefreshAll;
    }

    /// <summary>Pousse toutes les cellules <c>claude</c> connues hors cadence - la page Réglages (« Relire
    /// maintenant ») n'a pas de cellule à elle pour appeler <see cref="InvokeAsync"/>, contrairement à un clic
    /// sur la carte d'une pilule.</summary>
    public void RefreshAll()
    {
        string[] ids;
        lock (_lock) ids = _cells.ToArray();
        foreach (var id in ids) Push(id);
    }

    /// <summary>Posée par l'App (<c>ClaudeCli.SignIn</c>) : ouvre un terminal visible sur <c>claude auth
    /// login --claudeai</c>. Absente (tests, avant le plan 4) : l'action <c>sign-in</c> ne fait rien.</summary>
    public Action? SignIn { get; set; }

    public override string Type => "claude";

    public override SourceSchema Schema => new(Type, "Claude Code",
        new[]
        {
            new SchemaField("home", "path", "Dossier .claude (autre compte)", Required: false,
                Help: "Vide = %USERPROFILE%\\.claude"),
            new SchemaField("breakdown", "bool", "Répartition hebdomadaire par usage (Claude Code, Chats, Cowork…)",
                Default: "false"),
            new SchemaField("sessions", "bool", "Sessions Claude Code en cours (état dans la carte, pastille occupée / en attente)",
                Default: "false"),
        },
        "claude", "Usage (fenêtres, répartition hebdomadaire) et sessions Claude Code en cours");

    public override TimeSpan DefaultRefresh => TimeSpan.FromMinutes(5);

    /// <summary>L'instantané pour la page Réglages → Claude : jamais recalculé à la demande, juste ce que la
    /// dernière <see cref="ReadAsync"/> (de n'importe quelle cellule claude) a laissé.</summary>
    public ClaudeStatus Status
    {
        get { lock (_lock) return _status; }
    }

    public event Action? StatusChanged;

    public override async Task<Reading> ReadAsync(CellContext ctx, CancellationToken ct)
    {
        lock (_lock) _cells.Add(ctx.CellId);
        var dir = ctx.Str("home") ?? ClaudeCredentialsFile.DefaultDir();
        var showBreakdown = ctx.Flag("breakdown");
        var showSessions = ctx.Flag("sessions");
        var sessions = _sessions.Current;
        var nowMs = _now();

        // (2) Un 429 récent : ne pas retaper l'API avant l'attente calculée, jamais inventer un chiffre entre-temps.
        if (_backoffUntilMs is { } until && nowMs < until)
        {
            var reading = Stale(ctx.CellId, $"limite atteinte, nouvel essai à {Clock(until)}");
            PublishStatus(_readCredentials(dir), null, reading.Error, until, sessions);
            return reading;
        }

        // (3) Le jeton.
        var creds = _readCredentials(dir);
        if (creds is null)
        {
            var reading = ConnexionRequise();
            Remember(ctx.CellId, reading);
            PublishStatus(null, null, null, _backoffUntilMs, sessions);
            return reading;
        }

        if (creds.IsExpired(nowMs))
        {
            // Le délégué `findCli`/`runHidden` (posé par l'App) échappe au filtre de TokenRenewal pour tout
            // sauf une annulation : ReadAsync ne doit jamais tomber pour autant, un échec de renouvellement
            // n'est jamais qu'une raison de plus de rester périmé.
            try { await _renewal.TryRenewAsync().ConfigureAwait(false); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Warning("claude", $"renouvellement du jeton : {ex.Message}");
            }
            creds = _readCredentials(dir);
            if (creds is null || creds.IsExpired(_now()))
            {
                var reading = Stale(ctx.CellId, "jeton expiré — lance claude une fois");
                PublishStatus(creds, null, reading.Error, _backoffUntilMs, sessions);
                return reading;
            }
            lock (_lock) _lastRenewalMs = _now();
        }

        // (4) L'usage.
        try
        {
            var snapshot = await _usage.FetchAsync(creds.AccessToken, creds.SubscriptionType, ct).ConfigureAwait(false);
            return Succeed(ctx.CellId, creds, snapshot, sessions, _now(), showBreakdown, showSessions);
        }
        catch (UsageException ex)
        {
            return await HandleFailureAsync(ctx.CellId, ex, dir, creds, sessions, showBreakdown, showSessions, ct).ConfigureAwait(false);
        }
    }

    public override Task InvokeAsync(string action, CellContext ctx, CancellationToken ct)
    {
        switch (action)
        {
            case "refresh":
                Push(ctx.CellId);
                break;
            case "sign-in":
                SignIn?.Invoke();
                break;
        }
        return Task.CompletedTask;
    }

    // ---- Assemblage -------------------------------------------------------------------------------------

    private Reading Succeed(string cellId, ClaudeCredentials creds, UsageSnapshot snapshot, IReadOnlyList<ClaudeSession> sessions, long nowMs,
        bool showBreakdown, bool showSessions)
    {
        lock (_lock) { _failures = 0; _backoffUntilMs = null; }
        Backoff.Save(_backoffPath, null);
        var reading = BuildReading(snapshot, sessions, nowMs, showBreakdown, showSessions);
        Remember(cellId, reading);
        PublishStatus(creds, snapshot, null, null, sessions);
        return reading;
    }

    private async Task<Reading> HandleFailureAsync(string cellId, UsageException ex, string dir, ClaudeCredentials creds,
        IReadOnlyList<ClaudeSession> sessions, bool showBreakdown, bool showSessions, CancellationToken ct)
    {
        switch (ex.Kind)
        {
            case UsageFailure.RateLimited:
            {
                int failures;
                lock (_lock) failures = ++_failures;
                var until = _now() + Backoff.NextMs(failures, ex.RetryAfterMs);
                lock (_lock) _backoffUntilMs = until;
                Backoff.Save(_backoffPath, until);
                var reading = Stale(cellId, $"limite atteinte, nouvel essai à {Clock(until)}");
                PublishStatus(creds, null, reading.Error, until, sessions);
                return reading;
            }
            case UsageFailure.Unauthorized:
            {
                // Le jeton en main est refusé : on relit le fichier une fois (une autre tentative de
                // renouvellement a pu passer entre-temps) avant de conclure à une reconnexion.
                var reread = _readCredentials(dir);
                if (reread is not null && !reread.IsExpired(_now()) && reread.AccessToken != creds.AccessToken)
                {
                    try
                    {
                        var snapshot = await _usage.FetchAsync(reread.AccessToken, reread.SubscriptionType, ct).ConfigureAwait(false);
                        return Succeed(cellId, reread, snapshot, sessions, _now(), showBreakdown, showSessions);
                    }
                    catch (UsageException) { /* retombe sur « Connexion requise » ci-dessous */ }
                }
                var reading = ConnexionRequise();
                Remember(cellId, reading);
                PublishStatus(reread, null, "jeton refusé — connexion requise", _backoffUntilMs, sessions);
                return reading;
            }
            case UsageFailure.NoLimits:
            {
                var reading = new Reading(Status: CoreStatus.Off, Text: "Aucune limite rapportée", Actions: RefreshAndSignIn());
                Remember(cellId, reading);
                PublishStatus(creds, null, null, _backoffUntilMs, sessions);
                return reading;
            }
            default: // Network
            {
                var reading = Stale(cellId, ex.Message);
                PublishStatus(creds, null, reading.Error, _backoffUntilMs, sessions);
                return reading;
            }
        }
    }

    /// <summary>Fenêtres (toujours) puis répartition (<paramref name="showBreakdown"/>, libellés préfixés « · »)
    /// puis sessions (<paramref name="showSessions"/>) - dans cet ordre, comme la carte les montre. Le user ne veut
    /// par défaut que la consommation ; répartition et sessions sont des options de la cellule (ou du réglage
    /// global <c>sources.claude</c>). <see cref="Reading.Status"/> : Attention si une session attend, Busy si une
    /// travaille, sinon <c>null</c> (les seuils de l'anneau décident depuis Value/Max) - et toujours <c>null</c>
    /// quand <paramref name="showSessions"/> est faux, pour que la pastille ne bouge plus avec des sessions que la
    /// carte ne montre pas.</summary>
    private static Reading BuildReading(UsageSnapshot snapshot, IReadOnlyList<ClaudeSession> sessions, long nowMs,
        bool showBreakdown, bool showSessions)
    {
        var headline = UsageParser.Headline(snapshot.Windows);
        var detail = new List<DetailRow>(snapshot.Windows.Count
            + (showBreakdown ? snapshot.Breakdown.Count : 0) + (showSessions ? sessions.Count : 0));

        foreach (var w in snapshot.Windows)
            detail.Add(new DetailRow(w.Label, FormatPercent(w.Percent), Clamp01(w.Percent / 100),
                w.ResetsAtMs is { } resetsAtMs ? $"reset le {ResetClock(resetsAtMs)}" : null));

        if (showBreakdown)
            foreach (var b in snapshot.Breakdown)
                detail.Add(new DetailRow("· " + b.Label, FormatPercent(b.Percent)));

        if (showSessions)
            foreach (var s in sessions)
                detail.Add(new DetailRow(s.Name, SessionText(s, nowMs), Tone: ToneOf(s.State)));

        var status = !showSessions ? (CoreStatus?)null
            : sessions.Any(s => s.State == SessionState.Waiting) ? CoreStatus.Attention
            : sessions.Any(s => s.State == SessionState.Busy) ? CoreStatus.Busy
            : (CoreStatus?)null;

        return new Reading(Value: headline?.Percent, Max: headline is null ? null : 100.0, Status: status,
            Detail: detail, Actions: RefreshAndSignIn());
    }

    private static Reading ConnexionRequise() => new(Status: CoreStatus.Off, Text: "Connexion requise",
        Detail: new[] { new DetailRow("Connexion", "Lance « Se connecter »") },
        Actions: new[] { new ActionSpec("sign-in", "Se connecter", "refresh") });

    private static IReadOnlyList<ActionSpec> RefreshAndSignIn() => new[]
    {
        new ActionSpec("refresh", "Actualiser", "refresh"),
        new ActionSpec("sign-in", "Se connecter", "refresh"),
    };

    private static string SessionText(ClaudeSession s, long nowMs) => s.State switch
    {
        SessionState.Busy => $"occupée depuis {Elapsed(nowMs - s.SinceMs)}",
        SessionState.Waiting => "en attente de toi",
        SessionState.Idle => "inactive",
        SessionState.Ended => $"terminée {CellViews.Age(nowMs - s.SinceMs)}",
        _ => "",
    };

    private static CoreStatus ToneOf(SessionState state) => state switch
    {
        SessionState.Busy => CoreStatus.Busy,
        SessionState.Waiting => CoreStatus.Attention,
        _ => CoreStatus.Off,
    };

    // ---- Dernière lecture connue, par cellule -----------------------------------------------------------

    private void Remember(string cellId, Reading reading)
    {
        lock (_lock) _last[cellId] = reading;
    }

    /// <summary>La dernière lecture connue de <paramref name="cellId"/>, marquée périmée avec la raison — ou une
    /// lecture vide si aucune n'a jamais réussi (jamais un chiffre inventé). Le résultat est mémorisé à son tour :
    /// un second échec de suite garde l'instant du <em>premier</em> (<see cref="Reading.AsStale"/> ne le déplace pas).</summary>
    private Reading Stale(string cellId, string reason)
    {
        Reading result;
        lock (_lock)
        {
            var last = _last.TryGetValue(cellId, out var r) ? r : Reading.Empty;
            result = last.AsStale(_now(), reason);
            _last[cellId] = result;
        }
        return result;
    }

    // ---- Statut pour Réglages ----------------------------------------------------------------------------

    private void PublishStatus(ClaudeCredentials? creds, UsageSnapshot? snapshot, string? error, long? nextAttemptMs,
        IReadOnlyList<ClaudeSession> sessions)
    {
        lock (_lock)
        {
            _status = _status with
            {
                Credentials = creds,
                LastSnapshot = snapshot ?? _status.LastSnapshot,
                LastError = error,
                NextAttemptMs = nextAttemptMs,
                CliPath = _renewal.CliPath,
                Sessions = sessions,
                LastRenewalMs = _lastRenewalMs,
            };
        }
        StatusChanged?.Invoke();
    }

    // ---- Formatage -----------------------------------------------------------------------------------------

    private static double Clamp01(double v) => Math.Clamp(v, 0, 1);

    private static string FormatPercent(double percent) => $"{Math.Round(percent)} %";

    private static string Clock(long ms) => DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime().ToString("HH:mm");

    /// <summary>« 26/09 à 12:59 » en heure locale : le <c>Hint</c> d'une fenêtre de limite (« reset le … »), lu
    /// directement au lieu d'un décompte qui se périmait dès l'instant suivant.</summary>
    private static string ResetClock(long ms) =>
        DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime().ToString("dd/MM 'à' HH:mm", CultureInfo.InvariantCulture);

    /// <summary>« 2 min », « 3 h »… depuis le début d'une session occupée (<see cref="SessionText"/>) - pas de
    /// préfixe, l'appelant pose « depuis » ou « il y a » selon le sens.</summary>
    private static string Elapsed(long ms)
    {
        var seconds = Math.Max(0, ms) / 1000;
        if (seconds < 60) return "quelques secondes";
        if (seconds < 3600) return $"{seconds / 60} min";
        if (seconds < 86400) return $"{seconds / 3600} h";
        return $"{seconds / 86400} j";
    }
}

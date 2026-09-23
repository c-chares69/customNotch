namespace CustomNotch.Core.Sources.Claude;

/// <summary>Ce qu'une tentative de renouvellement a produit. C# n'a pas d'union native : un enum fermé plus
/// une charge utile optionnelle (<see cref="UntilMs"/>/<see cref="Message"/>) joue le même rôle sans caster.</summary>
public enum RenewalKind { Idle, Renewed, Failed }

public sealed record RenewalOutcome(RenewalKind Kind, long? UntilMs = null, string? Message = null)
{
    public static readonly RenewalOutcome Idle = new(RenewalKind.Idle);
    public static RenewalOutcome Renewed(long untilMs) => new(RenewalKind.Renewed, UntilMs: untilMs);
    public static RenewalOutcome Failed(string message) => new(RenewalKind.Failed, Message: message);
}

/// <summary>Renouvelle le jeton en lançant <c>claude -p</c> en tâche de fond quand l'expiration approche : un
/// seul essai par jeton (<c>attemptedFor</c>), puis une attente doublée par échec avant de retenter le même
/// jeton. Lecture, recherche du CLI, lancement et horloge sont injectés - testable sans processus réel.</summary>
public sealed class TokenRenewal(Func<ClaudeCredentials?> read, Func<string?> findCli, Func<string, Task<int>> runHidden, Func<long> now)
{
    /// <summary>Sous ce seuil avant expiration, on tente un renouvellement.</summary>
    public const long MarginMs = 4 * 60_000;
    /// <summary>Attente avant de retenter le même jeton après un premier échec.</summary>
    public const long CooldownMs = 10 * 60_000;
    public const long CooldownCapMs = 3_600_000;

    private readonly List<int> _launchedPids = new();
    private long? _attemptedForMs;
    private long? _lastAttemptMs;
    private int _failures;

    /// <summary>Les pids lancés pour un renouvellement : <c>SessionRegistry.IgnoredPids</c> s'en sert pour ne
    /// pas les compter comme des sessions Claude Code de l'utilisateur.</summary>
    public IReadOnlyCollection<int> LaunchedPids => _launchedPids;
    public string? CliPath { get; private set; }

    /// <summary>À passer comme callback « launched » du <c>runHidden</c> fourni par l'App (<c>ClaudeCli.RunHiddenAsync</c>) :
    /// le pid rejoint <see cref="LaunchedPids"/> sans que TokenRenewal connaisse la façon dont le processus est lancé.</summary>
    public void NoteLaunched(int pid) => _launchedPids.Add(pid);

    /// <summary>Pur : pas de jeton → non ; hors marge → non ; jeton déjà tenté → non tant que le cooldown
    /// (doublé par échec, plafonné) n'est pas écoulé ; sinon oui.</summary>
    public static bool ShouldRenew(long? expiresAtMs, long nowMs, long? attemptedForMs, long? lastAttemptMs, int failures)
    {
        if (expiresAtMs is null) return false;
        if (expiresAtMs.Value - nowMs >= MarginMs) return false;
        if (attemptedForMs != expiresAtMs.Value) return true;
        if (lastAttemptMs is null) return false;
        var exponent = Math.Min(30, Math.Max(0, failures - 1));
        var cooldown = Math.Min(CooldownMs * (1L << exponent), CooldownCapMs);
        return nowMs - lastAttemptMs.Value >= cooldown;
    }

    public async Task<RenewalOutcome> TryRenewAsync()
    {
        var before = read();
        var nowMs = now();
        if (before is null || !ShouldRenew(before.ExpiresAtMs, nowMs, _attemptedForMs, _lastAttemptMs, _failures))
            return RenewalOutcome.Idle;

        var cli = findCli();
        CliPath = cli;
        _attemptedForMs = before.ExpiresAtMs;
        _lastAttemptMs = nowMs;
        if (cli is null)
        {
            _failures++;
            return RenewalOutcome.Failed("CLI Claude introuvable");
        }

        try { await runHidden(cli).ConfigureAwait(false); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _failures++;
            return RenewalOutcome.Failed(ex.Message);
        }

        var after = read();
        if (after is not null && after.ExpiresAtMs > before.ExpiresAtMs)
        {
            _failures = 0;
            return RenewalOutcome.Renewed(after.ExpiresAtMs);
        }
        _failures++;
        return RenewalOutcome.Failed("le jeton n'a pas changé après le renouvellement");
    }
}

using CustomNotch.Core.Sources.Claude;
using Xunit;
namespace CustomNotch.Core.Tests.Sources.Claude;

public class TokenRenewalTests
{
    private const long Now = 1_800_000_000_000;

    [Fact]
    public void Sans_expiration_connue_on_ne_renouvelle_pas()
    {
        Assert.False(TokenRenewal.ShouldRenew(null, Now, null, null, 0));
    }

    [Fact]
    public void Dix_minutes_restantes_on_ne_renouvelle_pas()
    {
        Assert.False(TokenRenewal.ShouldRenew(Now + 10 * 60_000, Now, null, null, 0));
    }

    [Fact]
    public void Trois_minutes_restantes_jamais_tente_on_renouvelle()
    {
        Assert.True(TokenRenewal.ShouldRenew(Now + 3 * 60_000, Now, null, null, 0));
    }

    [Fact]
    public void Meme_jeton_deja_tente_a_l_instant_on_ne_reessaie_pas()
    {
        var expiresAt = Now + 3 * 60_000;
        Assert.False(TokenRenewal.ShouldRenew(expiresAt, Now, attemptedForMs: expiresAt, lastAttemptMs: Now, failures: 1));
    }

    [Fact]
    public void Meme_jeton_tente_deux_minutes_plus_tot_avec_un_echec_reste_en_cooldown()
    {
        var expiresAt = Now + 3 * 60_000;
        var lastAttempt = Now - 2 * 60_000;
        Assert.False(TokenRenewal.ShouldRenew(expiresAt, Now, attemptedForMs: expiresAt, lastAttemptMs: lastAttempt, failures: 1));
    }

    [Fact]
    public void Meme_jeton_apres_le_cooldown_on_reessaie()
    {
        var expiresAt = Now + 3 * 60_000;
        var lastAttempt = Now - TokenRenewal.CooldownMs;
        Assert.True(TokenRenewal.ShouldRenew(expiresAt, Now, attemptedForMs: expiresAt, lastAttemptMs: lastAttempt, failures: 1));
    }

    [Fact]
    public async Task Expiration_inchangee_apres_le_run_rend_failed()
    {
        var credentials = new ClaudeCredentials("t", Now + 3 * 60_000, null, null);
        var renewal = new TokenRenewal(() => credentials, () => "claude", _ => Task.FromResult(0), () => Now);

        var outcome = await renewal.TryRenewAsync();

        Assert.Equal(RenewalKind.Failed, outcome.Kind);
        Assert.NotNull(outcome.Message);
    }

    [Fact]
    public async Task Expiration_avancee_apres_le_run_rend_renewed()
    {
        var before = new ClaudeCredentials("t", Now + 3 * 60_000, null, null);
        var after = new ClaudeCredentials("t2", Now + 5 * 60 * 60_000, null, null);
        var reads = new Queue<ClaudeCredentials>(new[] { before, after });
        var renewal = new TokenRenewal(() => reads.Dequeue(), () => "claude", _ => Task.FromResult(0), () => Now);

        var outcome = await renewal.TryRenewAsync();

        Assert.Equal(RenewalKind.Renewed, outcome.Kind);
        Assert.Equal(after.ExpiresAtMs, outcome.UntilMs);
    }

    [Fact]
    public async Task Hors_marge_ne_lance_pas_le_cli()
    {
        var credentials = new ClaudeCredentials("t", Now + 60 * 60_000, null, null);
        var called = false;
        var renewal = new TokenRenewal(() => credentials, () => "claude", _ => { called = true; return Task.FromResult(0); }, () => Now);

        var outcome = await renewal.TryRenewAsync();

        Assert.Equal(RenewalKind.Idle, outcome.Kind);
        Assert.False(called);
    }

    [Fact]
    public void NoteLaunched_alimente_LaunchedPids()
    {
        var renewal = new TokenRenewal(() => null, () => null, _ => Task.FromResult(0), () => Now);
        renewal.NoteLaunched(4321);
        Assert.Contains(4321, renewal.LaunchedPids);
    }
}

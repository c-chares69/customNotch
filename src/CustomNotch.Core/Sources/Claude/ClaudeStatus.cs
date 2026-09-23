namespace CustomNotch.Core.Sources.Claude;

/// <summary>L'instantané que lit la page Réglages → Claude : ce que <see cref="ClaudeSource"/> sait à
/// l'instant, sans qu'elle ait à relire un fichier ou refaire un appel réseau. <see cref="LastSnapshot"/>
/// reste la dernière réponse d'usage reçue même après une lecture en échec (jamais effacée par une erreur
/// passagère) ; <see cref="LastError"/>/<see cref="NextAttemptMs"/> ne décrivent que la dernière tentative.</summary>
public sealed record ClaudeStatus(
    ClaudeCredentials? Credentials,
    UsageSnapshot? LastSnapshot,
    string? LastError,
    long? NextAttemptMs,
    string? CliPath,
    IReadOnlyList<ClaudeSession> Sessions,
    long? LastRenewalMs);

using System.Text.Json;
using System.Text.Json.Nodes;

namespace CustomNotch.Core.Sources.Claude;

/// <summary>Le jeton OAuth lu dans <c>.credentials.json</c> : jamais envoyé une fois expiré, jamais journalisé
/// en clair (<see cref="ClaudeCredentialsFile.Read"/> le masque avant de le rendre).</summary>
public sealed record ClaudeCredentials(string AccessToken, long ExpiresAtMs, string? SubscriptionType, string? RateLimitTier)
{
    public bool IsExpired(long nowMs) => ExpiresAtMs <= nowMs;
}

/// <summary>Lecture seule de <c>~/.claude/.credentials.json</c> (ou d'un dossier <c>home</c> alternatif, autre
/// compte) : jamais d'écriture, jamais d'exception qui remonte - un fichier absent ou illisible rend simplement
/// <c>null</c>, comme une session Claude Code jamais lancée sur ce poste.</summary>
public static class ClaudeCredentialsFile
{
    public static string DefaultDir()
    {
        var userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
        if (string.IsNullOrWhiteSpace(userProfile))
            userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, ".claude");
    }

    public static ClaudeCredentials? Read(string dir)
    {
        var path = Path.Combine(dir, ".credentials.json");
        try
        {
            var text = File.ReadAllText(path);
            if (JsonNode.Parse(text)?["claudeAiOauth"] is not JsonObject oauth) return null;
            if (oauth["accessToken"] is not JsonValue tokenNode || !tokenNode.TryGetValue<string>(out var token) || token.Length == 0)
                return null;
            if (oauth["expiresAt"] is not JsonValue expNode || !expNode.TryGetValue<long>(out var expiresAtMs))
                return null;

            // Le jeton ne doit jamais atteindre le journal, même caviardé par accident dans un message qui
            // le contiendrait tel quel (Log.Redact ne connaît que les formes Bearer/pk_/sk-/ya29 par motif).
            Log.Mask(new[] { token });

            var subscriptionType = oauth["subscriptionType"] is JsonValue sv && sv.TryGetValue<string>(out var s) ? s : null;
            var rateLimitTier = oauth["rateLimitTier"] is JsonValue rv && rv.TryGetValue<string>(out var r) ? r : null;
            return new ClaudeCredentials(token, expiresAtMs, subscriptionType, rateLimitTier);
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
        catch (JsonException) { return null; }
    }
}

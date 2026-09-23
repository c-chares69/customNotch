using System.Text.Json;
using System.Text.Json.Nodes;

namespace CustomNotch.Core.Sources.Claude;

/// <summary>L'attente entre deux essais après un 429 : <c>Retry-After</c> s'il est fourni, sinon 60 s, doublée
/// à chaque récidive et plafonnée à 1 h - persistée (<see cref="Save"/>/<see cref="Load"/>) pour survivre à un
/// redémarrage de l'app plutôt que de retaper l'API tout de suite.</summary>
public static class Backoff
{
    private const long BaseMs = 60_000;
    public const long CapMs = 3_600_000;

    public static long NextMs(int failures, long? retryAfterMs)
    {
        var baseDelay = retryAfterMs ?? BaseMs;
        // L'exposant est plafonné avant le décalage binaire : à `failures` élevé, 1L << exponent déborderait
        // sinon largement au-delà du plafond avant même la multiplication.
        var exponent = Math.Min(30, Math.Max(0, failures - 1));
        var next = baseDelay * (1L << exponent);
        return Math.Min(next, CapMs);
    }

    public static long? Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var root = JsonNode.Parse(File.ReadAllText(path));
            return root?["until"] is JsonValue v && v.TryGetValue<long>(out var until) ? until : null;
        }
        catch (IOException) { return null; }
        catch (JsonException) { return null; }
    }

    public static void Save(string path, long? untilMs)
    {
        var obj = new JsonObject { ["until"] = untilMs };
        Json.WriteAtomic(path, Json.Format(obj), "claude");
    }
}

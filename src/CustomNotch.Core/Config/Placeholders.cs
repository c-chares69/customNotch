using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CustomNotch.Core.Config;

/// <summary>Où les secrets sont lus - secrets.json (DPAPI) dans l'application, un dictionnaire dans les tests.</summary>
public interface ISecretStore
{
    string? Get(string name);
}

/// <summary>Résolution de ${env:NAME}, ${secret:name} et ${home} dans toutes les chaînes d'un document JSON, pour que
/// cells.json reste portable : aucun chemin de machine ni secret n'y est écrit en clair.</summary>
public static partial class Placeholders
{
    [GeneratedRegex(@"\$\{(env|secret|home)(?::([^}]+))?\}")]
    private static partial Regex Pattern();

    public static (JsonNode? Result, List<string> Missing, List<string> SecretValues) Resolve(JsonNode? node, Func<string, string?> env, ISecretStore secrets, string home)
    {
        var missing = new List<string>();
        var secretValues = new List<string>();
        return (Walk(node, env, secrets, home, missing, secretValues), missing, secretValues);
    }

    private static JsonNode? Walk(JsonNode? node, Func<string, string?> env, ISecretStore secrets, string home, List<string> missing, List<string> secretValues)
    {
        switch (node)
        {
            case JsonObject obj:
                var o = new JsonObject();
                foreach (var (k, v) in obj) o[k] = Walk(v, env, secrets, home, missing, secretValues);
                return o;
            case JsonArray arr:
                var a = new JsonArray();
                foreach (var v in arr) a.Add(Walk(v, env, secrets, home, missing, secretValues));
                return a;
            case JsonValue value when value.TryGetValue<string>(out var s):
                return JsonValue.Create(Pattern().Replace(s, m =>
                {
                    var kind = m.Groups[1].Value;
                    var name = m.Groups[2].Value;
                    var replacement = kind switch
                    {
                        "home" => home,
                        "env" => env(name),
                        _ => secrets.Get(name),
                    };
                    if (replacement is null) missing.Add($"{kind}:{name}");
                    else if (kind == "secret") secretValues.Add(replacement);
                    return replacement ?? "";
                }));
            default:
                return node?.DeepClone();
        }
    }
}

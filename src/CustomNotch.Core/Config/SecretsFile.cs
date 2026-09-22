using System.Text.Json.Nodes;

namespace CustomNotch.Core.Config;

/// <summary>secrets.json : { "clickup_token": "dpapi:…" }. Jamais synchronisé, chiffré avec le compte Windows ; un blob
/// d'une autre machine se lit vide et la fenêtre de réglages redemande la valeur.</summary>
public sealed class SecretsFile : ISecretStore
{
    private readonly string _path;
    private readonly JsonObject _root;

    public SecretsFile(string path)
    {
        _path = path;
        _root = new JsonObject();
        try
        {
            if (File.Exists(path) && JsonNode.Parse(File.ReadAllText(path)) is JsonObject o) _root = o;
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
        {
            Log.Warning("secrets", $"secrets.json illisible : {ex.Message}");
        }
    }

    public IEnumerable<string> Names => _root.Select(kv => kv.Key).ToList();

    public string? Get(string name)
    {
        if (_root[name] is not JsonValue v || !v.TryGetValue<string>(out var stored)) return null;
        var clear = Secrets.Unseal(stored);
        return clear.Length == 0 ? null : clear;
    }

    public bool Set(string name, string value)
    {
        _root[name] = Secrets.Seal(value);
        return Json.WriteAtomic(_path, Json.Format(_root), "secrets");
    }

    public bool Remove(string name)
    {
        _root.Remove(name);
        return Json.WriteAtomic(_path, Json.Format(_root), "secrets");
    }
}

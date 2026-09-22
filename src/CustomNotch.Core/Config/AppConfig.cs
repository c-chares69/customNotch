using System.Text.Json.Nodes;

namespace CustomNotch.Core.Config;

/// <summary>config.json : l'état de l'application (chemin de cells.json, thème, langue, mises à jour). Clés à points,
/// même petit contrat que la Config de ClickUp-Extended pour que Theme.cs se copie tel quel.</summary>
public sealed class AppConfig
{
    private readonly string _path;
    private readonly JsonObject _root;

    public AppConfig(string path)
    {
        _path = path;
        _root = Load(path);
    }

    private static JsonObject Load(string path)
    {
        try
        {
            if (File.Exists(path) && JsonNode.Parse(File.ReadAllText(path)) is JsonObject o) return o;
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
        {
            Log.Warning("config", $"config.json illisible, valeurs par défaut : {ex.Message}");
        }
        return new JsonObject();
    }

    private JsonNode? Node(string dotted)
    {
        JsonNode? node = _root;
        foreach (var part in dotted.Split('.'))
        {
            node = node is JsonObject o ? o[part] : null;
            if (node is null) return null;
        }
        return node;
    }

    public string GetString(string key, string fallback = "") => Node(key) is JsonValue v && v.TryGetValue<string>(out var s) ? s : fallback;
    public bool GetBool(string key, bool fallback = false) => Node(key) is JsonValue v && v.TryGetValue<bool>(out var b) ? b : fallback;
    public double GetDouble(string key, double fallback = 0) => Node(key) is JsonValue v && v.TryGetValue<double>(out var d) ? d : fallback;
    public int GetInt(string key, int fallback = 0) => (int)GetDouble(key, fallback);

    public void Set(string key, object? value)
    {
        var parts = key.Split('.');
        var node = _root;
        foreach (var part in parts[..^1])
        {
            if (node[part] is not JsonObject child) node[part] = child = new JsonObject();
            node = child;
        }
        node[parts[^1]] = value switch
        {
            null => null,
            string s => JsonValue.Create(s),
            bool b => JsonValue.Create(b),
            int i => JsonValue.Create(i),
            double d => JsonValue.Create(d),
            _ => JsonValue.Create(value.ToString()),
        };
    }

    public bool Save() => Json.WriteAtomic(_path, Json.Format(_root), "config");
}

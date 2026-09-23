using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CustomNotch.Core.Config;

/// <summary>Une configuration illisible ou invalide : le message porte l'origine (JSON malformé, champ hors norme),
/// jamais une pile d'appel à interpréter.</summary>
public sealed class ConfigException : Exception
{
    public ConfigException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>Lecture de cells.json : commentaires et virgules finales tolérés (c'est un fichier que l'on édite à la main),
/// noms de propriétés en camelCase, casse indifférente.</summary>
public static class CellsJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonDocumentOptions DocOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static JsonObject Parse(string text)
    {
        try
        {
            return JsonNode.Parse(text, null, DocOptions) as JsonObject ?? throw new ConfigException("JSON : un objet { … } était attendu à la racine.");
        }
        catch (JsonException ex)
        {
            throw new ConfigException($"JSON illisible : {ex.Message}", ex);
        }
    }

    public static CellsFile ToFile(JsonObject node)
    {
        try
        {
            var file = node.Deserialize<CellsFile>(Options) ?? new CellsFile();
            file.Appearance ??= new();
            return file;
        }
        catch (JsonException ex)
        {
            throw new ConfigException($"Configuration illisible : {ex.Message}", ex);
        }
    }

    public static string Serialize(object value) => JsonSerializer.Serialize(value, Options);
}

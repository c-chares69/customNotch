using System.Text.Json.Nodes;

namespace CustomNotch.Core.Sources;

/// <summary>Le strict nécessaire pour pointer une valeur dans un JSON : « data.items[1].n » ou « data.items.1.n ».
/// Pas de jokers ni de filtres - pour ça, la source shell et un jq.</summary>
public static class JsonPath
{
    public static JsonNode? Select(JsonNode? root, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return root;
        var node = root;
        foreach (var raw in path.Replace("[", ".").Replace("]", "").Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            switch (node)
            {
                case JsonArray arr when int.TryParse(raw, out var i):
                    node = i >= 0 && i < arr.Count ? arr[i] : null;
                    break;
                case JsonObject obj:
                    node = obj[raw];
                    break;
                default:
                    return null;
            }
            if (node is null) return null;
        }
        return node;
    }
}

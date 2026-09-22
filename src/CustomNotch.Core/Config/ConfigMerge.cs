using System.Text.Json.Nodes;

namespace CustomNotch.Core.Config;

/// <summary>cells.<machine>.json par-dessus cells.json : les objets se fusionnent clé par clé, les listes de pilules et
/// de cellules par « id ». Une pilule ou une cellule que le fichier partagé ne connaît pas est ignorée : la surcharge
/// locale ajuste, elle ne crée pas.</summary>
public static class ConfigMerge
{
    public static JsonObject Merge(JsonObject shared, JsonObject? local)
    {
        var result = shared.DeepClone().AsObject();
        if (local is null) return result;
        MergeObject(result, local);
        return result;
    }

    private static void MergeObject(JsonObject target, JsonObject overlay)
    {
        foreach (var (key, value) in overlay)
        {
            if (value is JsonObject o && target[key] is JsonObject t) MergeObject(t, o);
            else if (value is JsonArray a && target[key] is JsonArray ta && key is "pills" or "cells") MergeById(ta, a);
            else target[key] = value?.DeepClone();
        }
    }

    private static void MergeById(JsonArray target, JsonArray overlay)
    {
        foreach (var item in overlay.OfType<JsonObject>())
        {
            // « id » doit être une chaîne pour désigner une pilule ou une cellule ; un id d'un autre type
            // (nombre, objet…) dans la surcharge locale est ignoré plutôt que de faire planter GetValue<string>().
            if (item["id"] is not JsonValue v || !v.TryGetValue<string>(out var id)) continue;
            var existing = target.OfType<JsonObject>().FirstOrDefault(x => x["id"]?.GetValue<string>() == id);
            if (existing is not null) MergeObject(existing, item);
        }
    }
}

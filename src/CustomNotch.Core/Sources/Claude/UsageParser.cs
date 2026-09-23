using System.Globalization;
using System.Text.Json.Nodes;

namespace CustomNotch.Core.Sources.Claude;

/// <summary>Pur : un <see cref="JsonNode"/> entre, des fenêtres et une répartition dédoublonnées et
/// libellées en français sortent - rien qui touche le réseau ou l'horloge, testable sur des fixtures figées.</summary>
public static class UsageParser
{
    public static (List<LimitWindow> Windows, List<UsageBreakdown> Breakdown) Parse(JsonNode root)
    {
        var windows = new List<LimitWindow>();

        // L'API rend `limits[]` normalement, et toujours `five_hour`/`seven_day` en tête pour compatibilité -
        // les deux peuvent décrire la même fenêtre (même reset) : on les fusionne, on ne les additionne pas.
        if (root["limits"] is JsonArray limits)
            foreach (var item in limits)
                AddFromLimit(windows, item);

        AddFallback(windows, root["five_hour"] as JsonObject, "five_hour");
        AddFallback(windows, root["seven_day"] as JsonObject, "seven_day");

        var breakdown = new List<UsageBreakdown>();
        if (root["seven_day_breakdown"]?["rows"] is JsonArray rows)
            foreach (var row in rows)
            {
                if (row is not JsonObject o) continue;
                var label = ReadString(o["display_name"]) ?? ReadString(o["key"]);
                var percent = ReadPercent(o["percent"]);
                if (label is null || percent is null) continue;
                breakdown.Add(new UsageBreakdown(label, percent.Value));
            }

        return (windows, breakdown);
    }

    /// <summary>« Session (5 h) », « Semaine (tous modèles) », « Semaine (Fable) » (depuis
    /// <c>scope.model.display_name</c>), sinon le kind rendu lisible.</summary>
    public static string Label(string kind, string? model) => kind switch
    {
        "session" or "five_hour" => "Session (5 h)",
        "weekly_all" or "seven_day" or "weekly" => "Semaine (tous modèles)",
        "weekly_scoped" => model is { Length: > 0 } ? $"Semaine ({model})" : "Semaine",
        _ => Readable(kind),
    };

    /// <summary>La fenêtre à montrer en tête (anneau de la cellule) : la session si elle existe, sinon la
    /// première fenêtre connue.</summary>
    public static LimitWindow? Headline(IReadOnlyList<LimitWindow> w) =>
        w.FirstOrDefault(x => x.Id == "session") ?? w.FirstOrDefault(x => x.Id == "five_hour") ?? w.FirstOrDefault();

    private static void AddFromLimit(List<LimitWindow> windows, JsonNode? item)
    {
        if (item is not JsonObject o) return;
        var kind = ReadString(o["kind"]);
        var percent = ReadPercent(o["percent"]);
        if (kind is null || percent is null) return;
        var resetsAtMs = ParseResetsAt(o["resets_at"]);
        var model = ReadString(o["scope"]?["model"]?["display_name"]);
        Add(windows, kind, model, percent.Value, resetsAtMs);
    }

    private static void AddFallback(List<LimitWindow> windows, JsonObject? node, string kind)
    {
        if (node is null) return;
        var percent = ReadPercent(node["utilization"]);
        if (percent is null) return;
        var resetsAtMs = ParseResetsAt(node["resets_at"]);
        Add(windows, kind, null, percent.Value, resetsAtMs);
    }

    /// <summary>Dédoublonnage : même id d'alias (session/five_hour, weekly_all/seven_day/weekly), sinon même
    /// reset (à la seconde) et même pourcentage, sinon même libellé - dans cet ordre, la première entrée gagne.</summary>
    private static void Add(List<LimitWindow> windows, string kind, string? model, double percent, long? resetsAtMs)
    {
        var id = AliasId(kind);
        var label = Label(kind, model);
        var duplicate = windows.Any(w =>
            w.Id == id ||
            (resetsAtMs is not null && w.ResetsAtMs is not null && w.ResetsAtMs.Value / 1000 == resetsAtMs.Value / 1000 && w.Percent.Equals(percent)) ||
            w.Label == label);
        if (duplicate) return;
        windows.Add(new LimitWindow(id, label, percent, resetsAtMs));
    }

    private static string AliasId(string kind) => kind switch
    {
        "session" or "five_hour" => "session",
        "weekly_all" or "seven_day" or "weekly" => "weekly_all",
        _ => kind,
    };

    private static string Readable(string kind)
    {
        var words = kind.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return kind;
        words[0] = char.ToUpperInvariant(words[0][0]) + words[0][1..];
        return string.Join(' ', words);
    }

    private static string? ReadString(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static double? ReadPercent(JsonNode? node) => node is JsonValue v && v.TryGetValue<double>(out var d) ? d : null;

    private static long? ParseResetsAt(JsonNode? node)
    {
        var s = ReadString(node);
        if (s is null) return null;
        return DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto)
            ? dto.ToUnixTimeMilliseconds()
            : null;
    }
}

using System.Text;
using System.Text.Json.Nodes;
using CustomNotch.Core.Model;

namespace CustomNotch.Core.Sources;

/// <summary>Une URL, une extraction JSON, un maximum facultatif : de quoi afficher n'importe quel chiffre métier sans
/// écrire de code. Les en-têtes portent l'auth via ${secret:…}.</summary>
public sealed class HttpSource : SourceBase
{
    public static HttpClient Http { get; set; } = new() { Timeout = TimeSpan.FromSeconds(15) };

    public override string Type => "http";
    public override SourceSchema Schema => new(Type, "HTTP / JSON", new SchemaField[]
    {
        new("url", "url", "URL", Required: true, Group: "Requête"),
        new("method", "choice", "Méthode", Default: "GET", Choices: new[] { "GET", "POST" }, Group: "Requête"),
        new("path", "string", "Chemin JSON de la valeur", Help: "data.count ou items[0].n", Group: "Lecture"),
        new("textPath", "string", "Chemin JSON du texte", Group: "Lecture"),
        new("max", "number", "Maximum", Help: "Présent = anneau en %", Group: "Lecture"),
        new("unit", "string", "Unité", Group: "Lecture"),
        new("body", "string", "Corps (POST)", Group: "Requête"),
    }, "globe", "Un chiffre ou un texte lu dans une réponse JSON");
    public override TimeSpan DefaultRefresh => TimeSpan.FromMinutes(1);

    public override async Task<Reading> ReadAsync(CellContext ctx, CancellationToken ct)
    {
        var url = ctx.Str("url") ?? throw new InvalidOperationException("url manquante");
        using var request = new HttpRequestMessage(new HttpMethod(ctx.Str("method") ?? "GET"), url);
        request.Headers.TryAddWithoutValidation("User-Agent", $"{App.Name}/{App.Version}");
        // Le corps est construit avant les en-têtes : un Content-Type explicite dans « headers » doit pouvoir
        // remplacer le « application/json » par défaut, porté par les en-têtes du contenu, pas de la requête.
        if (ctx.Str("body") is { } body) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        if (ctx.Params["headers"] is JsonObject headers)
            foreach (var (hk, hv) in headers)
            {
                var val = hv?.ToString() ?? "";
                // TryAddWithoutValidation rend false pour un en-tête de contenu (Content-Type…) : il vit sur
                // request.Content.Headers, pas request.Headers - sinon la valeur est silencieusement perdue.
                if (!request.Headers.TryAddWithoutValidation(hk, val))
                {
                    if (request.Content is not null)
                    {
                        request.Content.Headers.Remove(hk);
                        request.Content.Headers.TryAddWithoutValidation(hk, val);
                    }
                    else Log.Warning("http", $"en-tête « {hk} » ignoré : pas de corps");
                }
            }
        using var response = await Http.SendAsync(request, ct).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"HTTP {(int)response.StatusCode}");
        JsonNode? root;
        try { root = JsonNode.Parse(text); }
        catch (global::System.Text.Json.JsonException) { root = JsonValue.Create(text); }
        var valueNode = JsonPath.Select(root, ctx.Str("path") ?? "");
        double? value = valueNode is JsonValue v && v.TryGetValue<double>(out var d) ? d
            : double.TryParse(valueNode?.ToString(), global::System.Globalization.NumberStyles.Float, global::System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        var textValue = ctx.Str("textPath") is { } tp ? JsonPath.Select(root, tp)?.ToString() : value is null ? valueNode?.ToString() : null;
        return new Reading(Value: value, Max: ctx.Num("max"), Unit: ctx.Str("unit"), Text: textValue,
            Detail: new[] { new DetailRow(ctx.Str("label") ?? "Valeur", textValue ?? value?.ToString() ?? "—") });
    }
}

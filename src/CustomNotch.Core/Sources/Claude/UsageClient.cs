using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CustomNotch.Core.Sources.Claude;

/// <summary>Pourquoi l'appel a échoué : de quoi décider entre backoff persisté, reconnexion, ou lecture
/// périmée - jamais un chiffre inventé.</summary>
public enum UsageFailure { RateLimited, Unauthorized, NoLimits, Network }

/// <summary>Une erreur typée de <see cref="UsageClient"/> : <see cref="RetryAfterMs"/> (429) alimente
/// <see cref="Backoff"/> sans reparser la réponse HTTP plus haut dans la pile.</summary>
public sealed class UsageException(UsageFailure kind, string message, long? retryAfterMs = null) : Exception(message)
{
    public UsageFailure Kind { get; } = kind;
    public long? RetryAfterMs { get; } = retryAfterMs;
}

/// <summary>L'endpoint usage OAuth d'Anthropic : <c>HttpClient</c> et horloge injectés pour être appelé sur un
/// <c>HttpMessageHandler</c> factice en test, sans jamais toucher le réseau.</summary>
public sealed class UsageClient(HttpClient http, Func<long> now)
{
    public const string Endpoint = "https://api.anthropic.com/api/oauth/usage";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    public async Task<UsageSnapshot> FetchAsync(string token, string? plan, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(RequestTimeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation("anthropic-beta", "oauth-2025-04-20");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage response;
        try { response = await http.SendAsync(request, cts.Token).ConfigureAwait(false); }
        catch (HttpRequestException ex) { throw new UsageException(UsageFailure.Network, ex.Message); }
        catch (TaskCanceledException ex) { throw new UsageException(UsageFailure.Network, ex.Message); }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new UsageException(UsageFailure.Unauthorized, $"HTTP {(int)response.StatusCode}");
            if ((int)response.StatusCode == 429)
                throw new UsageException(UsageFailure.RateLimited, "HTTP 429", RetryAfterMs(response));

            string text;
            try { text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false); }
            catch (HttpRequestException ex) { throw new UsageException(UsageFailure.Network, ex.Message); }
            if (!response.IsSuccessStatusCode)
                throw new UsageException(UsageFailure.Network, $"HTTP {(int)response.StatusCode}");

            JsonNode? root;
            try { root = JsonNode.Parse(text); }
            catch (JsonException ex) { throw new UsageException(UsageFailure.Network, ex.Message); }

            var (windows, breakdown) = UsageParser.Parse(root ?? new JsonObject());
            if (windows.Count == 0) throw new UsageException(UsageFailure.NoLimits, "Aucune limite rapportée");
            return new UsageSnapshot(windows, breakdown, now(), plan);
        }
    }

    /// <summary>Retry-After en secondes (le cas usuel) ou en date HTTP ; null si absent - Backoff comble
    /// alors avec 60 s par défaut.</summary>
    private long? RetryAfterMs(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header is null) return null;
        if (header.Delta is { } delta) return (long)delta.TotalMilliseconds;
        if (header.Date is { } date) return Math.Max(0, date.ToUnixTimeMilliseconds() - now());
        return null;
    }
}

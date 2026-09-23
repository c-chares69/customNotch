using System.Net;
using System.Net.Http.Headers;
using System.Text;
using CustomNotch.Core.Sources.Claude;
using Xunit;
namespace CustomNotch.Core.Tests.Sources.Claude;

public class UsageClientTests
{
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            return Task.FromResult(respond(request));
        }
    }

    private static string Fixture() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Sources", "Claude", "usage-2026-09-23.json"));

    private static UsageClient Client(FakeHandler handler, long nowMs = 1_000_000) =>
        new(new HttpClient(handler), () => nowMs);

    [Fact]
    public async Task Une_reponse_200_rend_un_instantane()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(Fixture(), Encoding.UTF8, "application/json") });

        var snapshot = await Client(handler, 42).FetchAsync("token-abc", "pro", CancellationToken.None);

        Assert.Equal(3, snapshot.Windows.Count);
        Assert.Equal(42, snapshot.ReadAtMs);
        Assert.Equal("pro", snapshot.Plan);
    }

    [Fact]
    public async Task Les_en_tetes_d_authentification_sont_poses()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(Fixture(), Encoding.UTF8, "application/json") });

        await Client(handler).FetchAsync("token-xyz", null, CancellationToken.None);

        Assert.Equal("Bearer", handler.LastRequest!.Headers.Authorization!.Scheme);
        Assert.Equal("token-xyz", handler.LastRequest.Headers.Authorization.Parameter);
        Assert.Equal("oauth-2025-04-20", handler.LastRequest.Headers.GetValues("anthropic-beta").Single());
    }

    [Fact]
    public async Task Un_429_avec_retry_after_leve_rate_limited()
    {
        var handler = new FakeHandler(_ =>
        {
            var response = new HttpResponseMessage((HttpStatusCode)429) { Content = new StringContent("") };
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
            return response;
        });

        var ex = await Assert.ThrowsAsync<UsageException>(() => Client(handler).FetchAsync("t", null, CancellationToken.None));

        Assert.Equal(UsageFailure.RateLimited, ex.Kind);
        Assert.Equal(30_000, ex.RetryAfterMs);
    }

    [Fact]
    public async Task Un_401_leve_unauthorized()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("") });

        var ex = await Assert.ThrowsAsync<UsageException>(() => Client(handler).FetchAsync("t", null, CancellationToken.None));

        Assert.Equal(UsageFailure.Unauthorized, ex.Kind);
    }

    [Fact]
    public async Task Un_200_sans_fenetre_leve_no_limits()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent("{}", Encoding.UTF8, "application/json") });

        var ex = await Assert.ThrowsAsync<UsageException>(() => Client(handler).FetchAsync("t", null, CancellationToken.None));

        Assert.Equal(UsageFailure.NoLimits, ex.Kind);
    }

    [Fact]
    public async Task Une_erreur_reseau_leve_network()
    {
        var handler = new FakeHandler(_ => throw new HttpRequestException("boom"));

        var ex = await Assert.ThrowsAsync<UsageException>(() => Client(handler).FetchAsync("t", null, CancellationToken.None));

        Assert.Equal(UsageFailure.Network, ex.Kind);
    }
}

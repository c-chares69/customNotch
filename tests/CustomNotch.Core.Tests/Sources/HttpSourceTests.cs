using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using CustomNotch.Core.Sources;
using Xunit;
namespace CustomNotch.Core.Tests.Sources;

public class HttpSourceTests : IDisposable
{
    private readonly HttpListener _server = new();
    private readonly string _prefix;
    private string _body = "{}";
    private string? _seenAuth;

    public HttpSourceTests()
    {
        var port = Random.Shared.Next(20000, 40000);
        _prefix = $"http://127.0.0.1:{port}/";
        _server.Prefixes.Add(_prefix);
        _server.Start();
        _ = Task.Run(async () =>
        {
            while (_server.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = await _server.GetContextAsync(); } catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException) { return; }
                _seenAuth = ctx.Request.Headers["Authorization"];
                var bytes = Encoding.UTF8.GetBytes(ctx.Request.Url!.AbsolutePath == "/fail" ? "boom" : _body);
                ctx.Response.StatusCode = ctx.Request.Url.AbsolutePath == "/fail" ? 500 : 200;
                await ctx.Response.OutputStream.WriteAsync(bytes);
                ctx.Response.Close();
            }
        });
    }

    public void Dispose() => _server.Stop();

    private CellContext Ctx(string paramsJson) => new("h", JsonNode.Parse(paramsJson)!.AsObject(), null);

    [Fact]
    public async Task Une_valeur_est_extraite_avec_max_et_unite()
    {
        _body = """{"data":{"open":12,"limit":40}}""";
        var r = await new HttpSource().ReadAsync(Ctx($$$"""{"url":"{{{_prefix}}}x","path":"data.open","max":40,"unit":"tickets","headers":{"Authorization":"Bearer t"}}"""), CancellationToken.None);
        Assert.Equal(12, r.Value);
        Assert.Equal(40, r.Max);
        Assert.Equal("tickets", r.Unit);
        Assert.Equal("Bearer t", _seenAuth);
    }

    [Fact]
    public async Task Un_texte_peut_etre_extrait_a_part()
    {
        _body = """{"status":"green","count":3}""";
        var r = await new HttpSource().ReadAsync(Ctx($$"""{"url":"{{_prefix}}","path":"count","textPath":"status"}"""), CancellationToken.None);
        Assert.Equal(3, r.Value);
        Assert.Equal("green", r.Text);
    }

    [Fact]
    public async Task Une_erreur_http_leve()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new HttpSource().ReadAsync(Ctx($$"""{"url":"{{_prefix}}fail"}"""), CancellationToken.None));
        Assert.Contains("500", ex.Message);
    }
}

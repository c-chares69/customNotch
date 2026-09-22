using System.Text.Json.Nodes;
using CustomNotch.Core.Sources;
using Xunit;
namespace CustomNotch.Core.Tests.Sources;

public class ShellSourceTests
{
    private static CellContext Ctx(string json) => new("s", JsonNode.Parse(json)!.AsObject(), null);

    [Fact]
    public async Task Un_nombre_est_lu_sur_la_sortie()
    {
        var r = await new ShellSource().ReadAsync(Ctx("""{"command":"echo 42","parse":"number","unit":"°C"}"""), CancellationToken.None);
        Assert.Equal(42, r.Value);
        Assert.Equal("°C", r.Unit);
    }

    [Fact]
    public async Task Un_json_est_lu_avec_un_chemin()
    {
        var r = await new ShellSource().ReadAsync(Ctx("""{"command":"echo {\"a\":{\"b\":5}}","parse":"json","path":"a.b"}"""), CancellationToken.None);
        Assert.Equal(5, r.Value);
    }

    [Fact]
    public async Task Le_texte_est_gardé_tel_quel()
    {
        var r = await new ShellSource().ReadAsync(Ctx("""{"command":"echo hello","parse":"text"}"""), CancellationToken.None);
        Assert.Equal("hello", r.Text);
    }

    [Fact]
    public async Task Un_code_de_sortie_non_nul_leve()
        => await Assert.ThrowsAsync<InvalidOperationException>(() => new ShellSource().ReadAsync(Ctx("""{"command":"exit 3"}"""), CancellationToken.None));

    [Fact]
    public async Task Le_delai_est_respecte()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new ShellSource().ReadAsync(Ctx("""{"command":"ping -n 6 127.0.0.1 > nul","timeoutSeconds":1}"""), CancellationToken.None));
        Assert.Contains("délai", ex.Message);
    }

    [Fact]
    public async Task Un_stderr_abondant_ne_bloque_pas()
    {
        var r = await new ShellSource().ReadAsync(Ctx("""{"command":"for /L %i in (1,1,3000) do @echo ligne %i 1>&2 & echo 7","parse":"number","timeoutSeconds":5}"""), CancellationToken.None);
        Assert.Equal(7, r.Value);
    }
}

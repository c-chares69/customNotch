using System.Text.Json.Nodes;
using CustomNotch.Core.Sources;
using Xunit;
namespace CustomNotch.Core.Tests.Sources;

public class JsonPathTests
{
    private static readonly JsonNode Doc = JsonNode.Parse("""{"data":{"count":7,"items":[{"n":"a"},{"n":"b"}],"x.y":1}}""")!;

    [Theory]
    [InlineData("data.count", "7")]
    [InlineData("data.items[1].n", "b")]
    [InlineData("data.items.0.n", "a")]
    public void La_notation_pointee_et_les_index_marchent(string path, string expected)
        => Assert.Equal(expected, JsonPath.Select(Doc, path)!.ToString());

    [Fact]
    public void Un_chemin_absent_rend_null()
    {
        Assert.Null(JsonPath.Select(Doc, "data.nope"));
        Assert.Null(JsonPath.Select(Doc, "data.items[9]"));
    }

    [Fact]
    public void Un_chemin_vide_rend_la_racine() => Assert.Same(Doc, JsonPath.Select(Doc, ""));
}

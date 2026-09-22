using System.Text.Json.Nodes;
using CustomNotch.Core.Config;
using Xunit;
namespace CustomNotch.Core.Tests.Config;

public class PlaceholdersTests
{
    private sealed class Secrets : ISecretStore
    {
        public string? Get(string name) => name == "token" ? "s3cr3t" : null;
    }

    private static (JsonNode?, List<string>) Run(string json)
        => Placeholders.Resolve(JsonNode.Parse(json), n => n == "USERPROFILE" ? @"C:\Users\x" : null, new Secrets(), @"C:\home");

    [Fact]
    public void Env_secret_et_home_sont_remplaces_partout()
    {
        var (node, missing) = Run("""{"a":"${env:USERPROFILE}\\bin","b":{"c":["Bearer ${secret:token}","${home}/x"]}}""");
        Assert.Empty(missing);
        Assert.Equal(@"C:\Users\x\bin", node!["a"]!.GetValue<string>());
        Assert.Equal("Bearer s3cr3t", node["b"]!["c"]![0]!.GetValue<string>());
        Assert.Equal(@"C:\home/x", node["b"]!["c"]![1]!.GetValue<string>());
    }

    [Fact]
    public void Un_placeholder_inconnu_est_signale_et_laisse_vide()
    {
        var (node, missing) = Run("""{"a":"${secret:absent}","b":"${env:NOPE}"}""");
        Assert.Equal(new[] { "secret:absent", "env:NOPE" }, missing);
        Assert.Equal("", node!["a"]!.GetValue<string>());
    }

    [Fact]
    public void Les_valeurs_non_texte_sont_intactes()
    {
        var (node, _) = Run("""{"n":3,"b":true,"x":null}""");
        Assert.Equal(3, node!["n"]!.GetValue<int>());
        Assert.True(node["b"]!.GetValue<bool>());
        Assert.Null(node["x"]);
    }
}

using System.Text.Json.Nodes;
using CustomNotch.Core;
using Xunit;
namespace CustomNotch.Core.Tests;

public class UpdatesTests
{
    private static readonly string Sha = new('a', 64);

    [Fact]
    public void ParseVersion_ignore_le_texte_et_s_arrete_a_trois_nombres()
    {
        Assert.Equal((0, 3, 1), Updates.ParseVersion("0.3.1+abc"));
    }

    [Fact]
    public void IsNewer_compare_les_versions()
    {
        Assert.True(Updates.IsNewer("0.3.2", "0.3.1"));
        Assert.False(Updates.IsNewer("0.3.1", "0.3.1"));
    }

    [Fact]
    public void ParseManifest_resout_une_url_relative_contre_celle_du_manifeste()
    {
        var raw = JsonNode.Parse($$"""{"version":"1.2.3","url":"downloads/x.zip","sha256":"{{Sha}}"}""");
        var release = Updates.ParseManifest(raw, "https://example.com/releases/latest.json");
        Assert.Equal("https://example.com/releases/downloads/x.zip", release.Url);
    }

    [Fact]
    public void ParseManifest_refuse_une_empreinte_mal_formee()
    {
        var raw = JsonNode.Parse("""{"version":"1.0.0","url":"https://example.com/x.zip","sha256":"pas-du-hex"}""");
        Assert.Throws<UpdateException>(() => Updates.ParseManifest(raw));
    }

    [Fact]
    public void ParseManifest_refuse_un_manifeste_sans_version()
    {
        var raw = JsonNode.Parse("""{"url":"https://example.com/x.zip"}""");
        Assert.Throws<UpdateException>(() => Updates.ParseManifest(raw));
    }

    [Fact]
    public void Installable_est_faux_sans_empreinte()
    {
        var release = new Release("1.0.0", "https://example.com/x.zip");
        Assert.False(release.Installable);
    }

    [Fact]
    public void InstallerCommand_lance_le_setup_en_silencieux_pour_un_exe()
    {
        var stage = Path.Combine(Path.GetTempPath(), "cn-" + Guid.NewGuid().ToString("N")[..8] + ".exe");
        File.WriteAllBytes(stage, Array.Empty<byte>());
        try
        {
            var command = Updates.InstallerCommand(stage);
            Assert.Equal(stage, command[0]);
            Assert.Equal("/VERYSILENT", command[1]);
        }
        finally { File.Delete(stage); }
    }
}

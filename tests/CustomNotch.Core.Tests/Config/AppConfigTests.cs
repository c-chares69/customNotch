using CustomNotch.Core.Config;
using Xunit;
namespace CustomNotch.Core.Tests.Config;

public class AppConfigTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "cn-" + Guid.NewGuid().ToString("N")[..8]);

    public AppConfigTests() => Directory.CreateDirectory(_home);
    public void Dispose() { try { Directory.Delete(_home, true); } catch (IOException) { } }

    [Fact]
    public void Set_aller_retour_pour_les_quatre_types()
    {
        var path = Path.Combine(_home, "config.json");
        var app = new AppConfig(path);
        app.Set("a.s", "x");
        app.Set("a.b", true);
        app.Set("a.i", 3);
        app.Set("a.d", 1.5);
        Assert.True(app.Save());

        var reloaded = new AppConfig(path);
        Assert.Equal("x", reloaded.GetString("a.s"));
        Assert.True(reloaded.GetBool("a.b"));
        Assert.Equal(3, reloaded.GetInt("a.i"));
        Assert.Equal(1.5, reloaded.GetDouble("a.d"));
    }

    [Fact]
    public void Un_config_json_illisible_rend_des_valeurs_par_defaut_sans_planter()
    {
        var path = Path.Combine(_home, "config.json");
        File.WriteAllText(path, "{");
        var app = new AppConfig(path);
        Assert.Equal("", app.GetString("a.s"));
        Assert.Equal("dflt", app.GetString("a.s", "dflt"));
        Assert.False(app.GetBool("a.b"));
    }
}

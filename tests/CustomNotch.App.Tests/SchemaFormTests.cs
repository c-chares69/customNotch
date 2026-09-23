using System.Text.Json.Nodes;
using Xunit;
using CustomNotch.App.Settings;
using CustomNotch.Core.Sources;
namespace CustomNotch.App.Tests;

public class SchemaFormTests
{
    private static SchemaField F(string type, string? def = null) => new("x", type, "X", Default: def);

    [Theory]
    [InlineData("string", SchemaForm.Kind.Text)] [InlineData("number", SchemaForm.Kind.Number)] [InlineData("bool", SchemaForm.Kind.Check)]
    [InlineData("choice", SchemaForm.Kind.Choice)] [InlineData("path", SchemaForm.Kind.Path)] [InlineData("url", SchemaForm.Kind.Url)]
    [InlineData("secret", SchemaForm.Kind.Secret)] [InlineData("zzz", SchemaForm.Kind.Text)]
    public void Chaque_type_a_son_controle(string type, SchemaForm.Kind expected) => Assert.Equal(expected, SchemaForm.ControlKind(F(type)));

    [Fact]
    public void La_valeur_initiale_vient_des_params_puis_du_defaut()
    {
        Assert.Equal("D:", SchemaForm.InitialText(F("string", "C:"), new JsonObject { ["x"] = "D:" }));
        Assert.Equal("C:", SchemaForm.InitialText(F("string", "C:"), null));
        Assert.Equal("", SchemaForm.InitialText(F("string"), new JsonObject()));
        Assert.Equal("3.5", SchemaForm.InitialText(F("number"), new JsonObject { ["x"] = 3.5 }));
    }

    [Fact]
    public void Un_secret_affiche_vide_mais_se_sait_defini()
    {
        var p = new JsonObject { ["x"] = "${secret:c.x}" };
        Assert.Equal("", SchemaForm.InitialText(F("secret"), p));
        Assert.True(SchemaForm.IsSecretSet(F("secret"), p));
        Assert.False(SchemaForm.IsSecretSet(F("secret"), new JsonObject { ["x"] = "" }));
    }

    [Fact]
    public void Les_nombres_acceptent_virgule_et_point()
    {
        Assert.Equal(1.5, SchemaForm.ParseNumber("1,5"));
        Assert.Equal(2, SchemaForm.ParseNumber(" 2 "));
        Assert.Null(SchemaForm.ParseNumber("deux"));
        Assert.Null(SchemaForm.ParseNumber(""));
    }
}

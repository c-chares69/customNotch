using CustomNotch.Core.Config;
using CustomNotch.Core.Sources;
using Xunit;
namespace CustomNotch.Core.Tests.Config;

public class ConfigValidationTests
{
    private static readonly HashSet<string> Known = new() { "system.cpu", "launcher" };

    private static readonly Dictionary<string, SourceSchema> Schemas = new()
    {
        ["http"] = new("http", "HTTP", new SchemaField[]
        {
            new("url", "url", "URL", Required: true), new("max", "number", "Max"), new("method", "choice", "Méthode", Choices: new[] { "GET", "POST" }), new("flag", "bool", "Drapeau"),
        }),
        ["launcher"] = new("launcher", "Lanceur", new[] { new SchemaField("open", "string", "Ouvrir", Required: true) }),
    };

    private static CellsFile File(string json) => CellsJson.ToFile(CellsJson.Parse(json));

    [Fact]
    public void Un_fichier_correct_ne_donne_aucune_erreur()
    {
        var f = File("""{"pills":[{"id":"p","edge":"right","cells":[{"id":"cpu","source":"system.cpu","refresh":"2s"},{"id":"g","children":["cpu"],"headline":"cpu"}]}]}""");
        Assert.Empty(ConfigValidation.Validate(f, Known));
    }

    [Fact]
    public void Les_fautes_sont_nommees()
    {
        var f = File("""{"pills":[{"id":"p","edge":"middle","along":3,"cells":[{"id":"a","source":"nope"},{"id":"a","source":"launcher","refresh":"vite","kind":"blob"},{"id":"g","children":["zz"],"headline":"a"}]},{"id":"p"}]}""");
        var errors = ConfigValidation.Validate(f, Known);
        Assert.Contains(errors, e => e.Contains("pills[1]") && e.Contains("id « p » en double"));
        Assert.Contains(errors, e => e.Contains("edge « middle »"));
        Assert.Contains(errors, e => e.Contains("along"));
        Assert.Contains(errors, e => e.Contains("source « nope » inconnue"));
        Assert.Contains(errors, e => e.Contains("cellule « a » en double"));
        Assert.Contains(errors, e => e.Contains("refresh « vite »"));
        Assert.Contains(errors, e => e.Contains("kind « blob »"));
        Assert.Contains(errors, e => e.Contains("enfant « zz » introuvable"));
        Assert.Contains(errors, e => e.Contains("headline « a » n'est pas un enfant"));
    }

    [Fact]
    public void Un_groupe_qui_se_contient_est_refuse()
    {
        var f = File("""{"pills":[{"id":"p","edge":"right","cells":[{"id":"g","children":["g"]}]}]}""");
        var errors = ConfigValidation.Validate(f, Known);
        Assert.Contains(errors, e => e.Contains("boucle dans children") && e.Contains("g → g"));
    }

    [Fact]
    public void Deux_groupes_qui_se_contiennent_sont_refuses()
    {
        var f = File("""{"pills":[{"id":"p","edge":"right","cells":[{"id":"a","children":["b"]},{"id":"b","children":["a"]}]}]}""");
        var errors = ConfigValidation.Validate(f, Known);
        Assert.Contains(errors, e => e.Contains("boucle dans children"));
    }

    [Fact]
    public void Le_parseur_accepte_commentaires_et_virgules_finales()
    {
        var f = File("""
            {
              // la pilule principale
              "pills": [ { "id": "p", "cells": [], }, ],
            }
            """);
        Assert.Single(f.Pills);
    }

    [Fact]
    public void Un_json_illisible_leve_une_exception_lisible()
    {
        var ex = Assert.Throws<ConfigException>(() => File("{"));
        Assert.Contains("JSON", ex.Message);
    }

    [Fact]
    public void Les_params_sont_verifies_d_apres_le_schema()
    {
        var f = File("""
            {"pills":[{"id":"p","cells":[
            {"id":"a","source":"http","params":{"url":"pas une url","max":"beaucoup","method":"PUT","flag":"oui"}},
            {"id":"b","source":"launcher"},
            {"id":"c","source":"http","params":{"url":"https://x","max":3,"method":"GET","flag":true}}]}]}
            """);
        var errors = ConfigValidation.Validate(f, Schemas);
        Assert.Contains(errors, e => e.Contains("cells[0].params.url"));
        Assert.Contains(errors, e => e.Contains("cells[0].params.max") && e.Contains("nombre"));
        Assert.Contains(errors, e => e.Contains("cells[0].params.method") && e.Contains("GET, POST"));
        Assert.Contains(errors, e => e.Contains("cells[0].params.flag"));
        Assert.Contains(errors, e => e.Contains("cells[1].params.open") && e.Contains("requis"));
        Assert.DoesNotContain(errors, e => e.Contains("cells[2]"));
    }

    [Fact]
    public void Une_source_inconnue_reste_signalee_avec_les_schemas()
    {
        var f = File("""{"pills":[{"id":"p","cells":[{"id":"a","source":"nope"}]}]}""");
        Assert.Contains(ConfigValidation.Validate(f, Schemas), e => e.Contains("source « nope » inconnue"));
    }
}

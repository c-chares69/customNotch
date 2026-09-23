using System.Text.Json.Nodes;
using Xunit;
using CustomNotch.Core;
using CustomNotch.Core.Config;
using CustomNotch.Core.Sources;
namespace CustomNotch.Core.Tests.Config;

public class ConfigEditorTests : IDisposable
{
    private static readonly IReadOnlyDictionary<string, SourceSchema> Schemas = CoreSources.Build().Schemas;
    private readonly string _home = Path.Combine(Path.GetTempPath(), "cn-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly ConfigStore _store;
    private readonly ConfigEditor _editor;

    public ConfigEditorTests()
    {
        Directory.CreateDirectory(_home);
        _store = new ConfigStore(_home, Schemas);
        Assert.True(_store.Load());
        _editor = new ConfigEditor(_store, Schemas);
    }

    public void Dispose() { _store.Dispose(); try { Directory.Delete(_home, true); } catch (IOException) { } }

    private string Shared() => File.ReadAllText(_store.CellsPath);

    [Fact]
    public void Ajouter_une_pilule_puis_une_cellule_avec_les_defauts_du_schema()
    {
        var pill = _editor.AddPill("left");
        Assert.Equal("pill", pill);
        var cell = _editor.AddCell(pill, "system.disk");
        Assert.Equal("disk-2", cell);   // « disk » existe déjà dans le défaut
        var added = _store.Current.Cell(cell)!;
        Assert.Equal("system.disk", added.Source);
        Assert.Equal("Disque", added.Label);
        Assert.Equal("disk", added.Glyph);
        Assert.Equal("C:", added.Params!["drive"]!.GetValue<string>());
        Assert.Equal("left", _store.Current.Pills[1].Edge);
        Assert.StartsWith("// customNotch", Shared());
    }

    [Fact]
    public void Modifier_deplacer_supprimer_une_cellule()
    {
        _editor.SetCell("media", c => c["label"] = "Musique");
        Assert.Equal("Musique", _store.Current.Cell("media")!.Label);
        _editor.MoveCell("media", -1);
        Assert.Equal("media", _store.Current.Pills[0].Cells[5].Id);
        _editor.RemoveCell("cpu");
        Assert.Null(_store.Current.Cell("cpu"));
        var sys = _store.Current.Cell("sys")!;
        Assert.DoesNotContain("cpu", sys.Children!);
        Assert.Null(sys.Headline);   // la tête était cpu
    }

    [Fact]
    public void Masquer_va_dans_le_fichier_local_pas_dans_le_partage()
    {
        var before = Shared();
        _editor.SetCellVisible("media", false);
        Assert.Equal(before, Shared());
        Assert.False(_store.Current.Cell("media")!.Visible);
        Assert.Contains("\"visible\": false", File.ReadAllText(_store.LocalPath));
    }

    [Fact]
    public void Deplacer_une_cellule_vers_une_autre_pilule()
    {
        var pill = _editor.AddPill("top");
        _editor.MoveCellToPill("clickup", pill);
        Assert.Equal("clickup", _store.Current.Pills[1].Cells.Single().Id);
        Assert.DoesNotContain(_store.Current.Pills[0].Cells, c => c.Id == "clickup");
    }

    [Fact]
    public void Une_modification_refusee_est_annulee_et_remontee()
    {
        var before = Shared();
        var ex = Assert.Throws<ConfigException>(() => _editor.SetCell("sys", c => c["children"] = new JsonArray("sys")));
        Assert.Contains("boucle", ex.Message);
        Assert.Equal(before, Shared());
        Assert.Contains("cpu", _store.Current.Cell("sys")!.Children!);
    }

    [Fact]
    public void Un_secret_va_dans_secrets_json_et_le_partage_porte_le_placeholder()
    {
        var cell = _editor.AddCell("main", "http");
        _editor.SetSecret(ConfigEditor.SecretName(cell, "token"), "s3cr3t");
        _editor.SetCell(cell, c => { c["params"]!["url"] = "https://x"; c["params"]!["headers"] = new JsonObject { ["Authorization"] = "Bearer " + ConfigEditor.SecretPlaceholder(cell, "token") }; });
        Assert.DoesNotContain("s3cr3t", Shared());
        Assert.Equal("Bearer s3cr3t", _store.Current.Cell(cell)!.Params!["headers"]!["Authorization"]!.GetValue<string>());
        _editor.RemoveSecret(ConfigEditor.SecretName(cell, "token"));
        Assert.Equal("Bearer ", _store.Current.Cell(cell)!.Params!["headers"]!["Authorization"]!.GetValue<string>());
    }

    [Fact]
    public void Supprimer_une_pilule_retire_aussi_sa_surcharge_locale()
    {
        var pill = _editor.AddPill("bottom");
        _editor.SetPillLocal(pill, p => p["along"] = 0.2);
        _editor.RemovePill(pill);
        Assert.Single(_store.Current.Pills);
        Assert.DoesNotContain($"\"{pill}\"", File.ReadAllText(_store.LocalPath));
    }

    [Fact]
    public void Un_reglage_global_de_source_est_ecrit_sous_sources()
    {
        _editor.SetSourceGlobal("http", g => g["timeoutSeconds"] = 30);
        Assert.Equal(30, _store.Current.Sources!["http"]!["timeoutSeconds"]!.GetValue<int>());
    }

    [Fact]
    public void Un_fichier_verrouille_fait_echouer_l_operation_au_lieu_de_l_ignorer()
    {
        using var lockHandle = new FileStream(_store.CellsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Throws<ConfigException>(() => _editor.AddPill("left"));
        Assert.Single(_store.Current.Pills);
    }

    [Fact]
    public void Un_secret_verrouille_leve()
    {
        _editor.SetSecret("a", "b");
        using var lockHandle = new FileStream(Paths.SecretsFile(_home), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Throws<ConfigException>(() => _editor.SetSecret("a", "c"));
    }

    [Fact]
    public void Un_fichier_local_verrouille_donne_un_message()
    {
        using var lockHandle = new FileStream(_store.LocalPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        var ex = Assert.Throws<ConfigException>(() => _editor.SetPillLocal("main", p => p["along"] = 0.2));
        Assert.NotEmpty(ex.Message);
    }

    [Fact]
    public void Un_fichier_local_illisible_n_est_pas_ecrase()
    {
        // Verrouillé pendant une synchronisation : rien ne doit être écrit, sinon un document neuf effacerait
        // les positions de toutes les autres pilules dès que le verrou tombe.
        _editor.SetPillLocal("main", p => p["along"] = 0.7);
        var before = File.ReadAllText(_store.LocalPath);
        using (new FileStream(_store.LocalPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.Throws<ConfigException>(() => _editor.SetPillLocal("main", p => p["along"] = 0.2));
        Assert.Equal(before, File.ReadAllText(_store.LocalPath));
    }

    [Fact]
    public void Retirer_un_secret_indispensable_est_annule()
    {
        // Un lanceur dont le champ requis « open » ne vit que par un secret : le retirer laisserait un
        // placeholder non résolu (champ présent mais vide), refusé par la validation sur un champ requis.
        var placeholder = ConfigEditor.SecretPlaceholder("c", "open");
        File.WriteAllText(_store.CellsPath, """{"pills":[{"id":"main","cells":[{"id":"c","source":"launcher","params":{"open":"PLACEHOLDER"}}]}]}""".Replace("PLACEHOLDER", placeholder));
        // Écrire le secret recharge tout de suite : c'est là que le placeholder se résout pour la première fois.
        _editor.SetSecret("c.open", "x");
        Assert.Equal("x", _store.Current.Cell("c")!.Params!["open"]!.GetValue<string>());

        var ex = Assert.Throws<ConfigException>(() => _editor.RemoveSecret("c.open"));
        Assert.NotEmpty(ex.Message);
        Assert.Contains("c.open", _store.Secrets.Names);
        Assert.True(new ConfigStore(_home, Schemas).Load());
    }
}

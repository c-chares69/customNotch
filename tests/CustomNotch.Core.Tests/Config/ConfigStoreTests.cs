using System.Text.Json.Nodes;
using CustomNotch.Core;
using CustomNotch.Core.Config;
using Xunit;
namespace CustomNotch.Core.Tests.Config;

public class ConfigStoreTests : IDisposable
{
    private static readonly HashSet<string> Known = new() { "system.cpu", "system.memory", "system.disk", "system.network", "launcher", "http" };
    private readonly string _home = Path.Combine(Path.GetTempPath(), "cn-" + Guid.NewGuid().ToString("N")[..8]);

    public ConfigStoreTests() => Directory.CreateDirectory(_home);
    public void Dispose() { try { Directory.Delete(_home, true); } catch (IOException) { } }

    [Fact]
    public void Le_premier_lancement_ecrit_un_cells_json_par_defaut()
    {
        var store = new ConfigStore(_home, Known);
        Assert.True(store.Load());
        Assert.True(File.Exists(Path.Combine(_home, "cells.json")));
        Assert.Single(store.Current.Pills);
        Assert.Equal("cpu", store.Current.Pills[0].Cells[0].Id);
    }

    [Fact]
    public void Le_chemin_de_cells_json_vient_de_config_json()
    {
        var elsewhere = Path.Combine(_home, "sync", "notch.json");
        var app = new AppConfig(Paths.ConfigFile(_home));
        app.Set("cells_path", elsewhere);
        app.Save();
        var store = new ConfigStore(_home, Known);
        store.Load();
        Assert.Equal(elsewhere, store.CellsPath);
        Assert.True(File.Exists(elsewhere));
    }

    [Fact]
    public void La_surcharge_locale_et_les_secrets_sont_appliques()
    {
        File.WriteAllText(Path.Combine(_home, "cells.json"), """{"pills":[{"id":"p","along":0.5,"cells":[{"id":"h","source":"http","params":{"url":"https://x","headers":{"Authorization":"Bearer ${secret:tok}"}}}]}]}""");
        File.WriteAllText(Paths.LocalCellsFile(_home), """{"pills":[{"id":"p","along":0.1}]}""");
        var store = new ConfigStore(_home, Known);
        store.Secrets.Set("tok", "abc");
        Assert.True(store.Load());
        Assert.Equal(0.1, store.Current.Pills[0].Along);
        Assert.Equal("Bearer abc", store.Current.Pills[0].Cells[0].Params!["headers"]!["Authorization"]!.GetValue<string>());
    }

    [Fact]
    public void Une_configuration_invalide_est_refusee_et_la_precedente_reste()
    {
        var store = new ConfigStore(_home, Known);
        store.Load();
        var before = store.Current;
        string? rejected = null;
        store.Rejected += m => rejected = m;
        File.WriteAllText(store.CellsPath, """{"pills":[{"id":"p","edge":"nowhere"}]}""");
        Assert.False(store.Load());
        Assert.Same(before, store.Current);
        Assert.Contains("edge", rejected);
    }

    [Fact]
    public void Un_id_non_texte_dans_la_surcharge_est_ignore_pas_un_plantage()
    {
        File.WriteAllText(Path.Combine(_home, "cells.json"), """{"pills":[{"id":"p","cells":[{"id":"cpu","source":"system.cpu"}]}]}""");
        File.WriteAllText(Paths.LocalCellsFile(_home), """{"pills":[{"id":2,"along":0.9}]}""");
        var store = new ConfigStore(_home, Known);
        Assert.True(store.Load());
        Assert.Equal("p", store.Current.Pills[0].Id);
        Assert.Equal(0.5, store.Current.Pills[0].Along);
    }

    [Fact]
    public void SetPillLocal_ecrit_dans_la_surcharge_pas_dans_le_partage()
    {
        var store = new ConfigStore(_home, Known);
        store.Load();
        var sharedBefore = File.ReadAllText(store.CellsPath);
        store.SetPillLocal("main", p => { p["along"] = 0.9; p["screen"] = @"\\.\DISPLAY2"; });
        Assert.Equal(sharedBefore, File.ReadAllText(store.CellsPath));
        var local = JsonNode.Parse(File.ReadAllText(store.LocalPath))!;
        Assert.Equal(0.9, local["pills"]![0]!["along"]!.GetValue<double>());
        Assert.Equal(0.9, store.Current.Pills[0].Along);
    }

    [Fact]
    public async Task Le_watcher_recharge_apres_une_ecriture()
    {
        using var store = new ConfigStore(_home, Known);
        store.Load();
        var changed = new TaskCompletionSource<CellsFile>(TaskCreationOptions.RunContinuationsAsynchronously);
        store.Changed += f => changed.TrySetResult(f);
        store.StartWatching();
        await Task.Delay(200);
        File.WriteAllText(store.CellsPath, """{"pills":[{"id":"only","cells":[]}]}""");
        var result = await changed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("only", result.Pills[0].Id);
    }

    [Fact]
    public async Task Dispose_pendant_une_ecriture_ne_plante_pas()
    {
        var store = new ConfigStore(_home, Known);
        store.Load();
        store.StartWatching();
        // L'abonnement se fait AVANT Dispose() : si le watcher a le temps de déclencher un rechargement
        // avant qu'on dispose, on le sait (le compteur a déjà bougé) plutôt que de passer le test à côté.
        var changedCount = 0;
        store.Changed += _ => changedCount++;
        File.WriteAllText(store.CellsPath, """{"pills":[{"id":"only","cells":[]}]}""");
        store.Dispose();
        var countAtDispose = changedCount;
        await Task.Delay(500);
        Assert.Equal(countAtDispose, changedCount);
    }
}

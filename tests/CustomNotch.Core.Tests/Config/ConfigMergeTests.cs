using System.Text.Json.Nodes;
using CustomNotch.Core.Config;
using Xunit;
namespace CustomNotch.Core.Tests.Config;

public class ConfigMergeTests
{
    private static JsonObject O(string json) => JsonNode.Parse(json)!.AsObject();

    [Fact]
    public void La_surcharge_locale_change_une_pilule_par_id_sans_toucher_aux_autres()
    {
        var shared = O("""{"pills":[{"id":"main","edge":"right","along":0.5,"cells":[{"id":"cpu","source":"system.cpu"}]},{"id":"side","edge":"left"}]}""");
        var local = O("""{"pills":[{"id":"main","along":0.2,"screen":"\\\\.\\DISPLAY2"}]}""");
        var merged = ConfigMerge.Merge(shared, local);
        var main = merged["pills"]![0]!;
        Assert.Equal(0.2, main["along"]!.GetValue<double>());
        Assert.Equal("right", main["edge"]!.GetValue<string>());
        Assert.Equal(@"\\.\DISPLAY2", main["screen"]!.GetValue<string>());
        Assert.Equal("cpu", main["cells"]![0]!["id"]!.GetValue<string>());
        Assert.Equal("left", merged["pills"]![1]!["edge"]!.GetValue<string>());
    }

    [Fact]
    public void Une_cellule_est_surchargee_par_id_et_les_params_fusionnes_en_profondeur()
    {
        var shared = O("""{"pills":[{"id":"p","cells":[{"id":"disk","source":"system.disk","params":{"drive":"C:","x":1}},{"id":"b"}]}]}""");
        var local = O("""{"pills":[{"id":"p","cells":[{"id":"disk","visible":false,"params":{"drive":"D:"}}]}]}""");
        var disk = ConfigMerge.Merge(shared, local)["pills"]![0]!["cells"]![0]!;
        Assert.False(disk["visible"]!.GetValue<bool>());
        Assert.Equal("D:", disk["params"]!["drive"]!.GetValue<string>());
        Assert.Equal(1, disk["params"]!["x"]!.GetValue<int>());
    }

    [Fact]
    public void Une_pilule_inconnue_du_fichier_partage_est_ignoree()
    {
        var merged = ConfigMerge.Merge(O("""{"pills":[{"id":"p"}]}"""), O("""{"pills":[{"id":"ghost","edge":"top"}]}"""));
        Assert.Single(merged["pills"]!.AsArray());
    }

    [Fact]
    public void Un_id_non_texte_dans_la_surcharge_est_ignore()
    {
        var shared = O("""{"pills":[{"id":"p","along":0.5}]}""");
        var local = O("""{"pills":[{"id":2,"along":0.9}]}""");
        var merged = ConfigMerge.Merge(shared, local);
        Assert.Equal(0.5, merged["pills"]![0]!["along"]!.GetValue<double>());
    }

    [Fact]
    public void Sans_surcharge_le_document_est_rendu_tel_quel()
    {
        var shared = O("""{"version":1,"pills":[]}""");
        Assert.Equal(shared.ToJsonString(), ConfigMerge.Merge(shared, null).ToJsonString());
    }
}

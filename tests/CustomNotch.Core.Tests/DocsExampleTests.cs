using CustomNotch.Core.Config;
using CustomNotch.Core.Sources;
using Xunit;
namespace CustomNotch.Core.Tests;

/// <summary>Garde-fou de documentation : docs/cells.example.json doit toujours être une configuration
/// chargeable telle quelle, comme le README le promet.</summary>
public class DocsExampleTests
{
    /// <summary>Remonte depuis le dossier de sortie des tests jusqu'à trouver CustomNotch.sln : robuste
    /// à l'endroit d'où dotnet test est lancé (bin/Debug/net10.0/… en local, un autre chemin en CI).</summary>
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CustomNotch.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("CustomNotch.sln introuvable depuis " + AppContext.BaseDirectory);
    }

    [Fact]
    public void Le_fichier_d_exemple_est_valide()
    {
        var path = Path.Combine(RepoRoot(), "docs", "cells.example.json");
        var text = File.ReadAllText(path);
        var file = CellsJson.ToFile(CellsJson.Parse(text));
        var knownSources = CoreSources.Build().Schemas;
        var errors = ConfigValidation.Validate(file, knownSources);
        Assert.True(errors.Count == 0, string.Join(" ; ", errors));
        Assert.Equal(2, file.Pills.Count);
        Assert.Contains(file.AllCells(), c => c.IsGroup && c.Id == "systeme");
    }
}

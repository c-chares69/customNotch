using System.IO.Compression;
using CustomNotch.Core;
using Xunit;
namespace CustomNotch.Core.Tests;

public class DiagnosticsTests
{
    [Fact]
    public void MakeReport_omet_toujours_secrets_json()
    {
        var home = Path.Combine(Path.GetTempPath(), "cn-diag-" + Guid.NewGuid().ToString("N")[..8]);
        var outDir = Path.Combine(Path.GetTempPath(), "cn-diag-out-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(home);
        Directory.CreateDirectory(Path.Combine(home, "logs"));
        File.WriteAllText(Path.Combine(home, "secrets.json"), """{"clickup.token":"dpapi:secret"}""");
        File.WriteAllText(Path.Combine(home, "cells.json"), """{"pills":[]}""");
        File.WriteAllText(Path.Combine(home, "logs", "journal.log"), "2026-09-23 INFO [app] démarrage\n");
        try
        {
            var report = Diagnostics.MakeReport(home, outDir);
            using var archive = ZipFile.OpenRead(report);
            var names = archive.Entries.Select(e => e.FullName).ToList();
            Assert.Contains("info.txt", names);
            Assert.Contains("cells.json", names);
            Assert.Contains("journal.log", names);
            Assert.DoesNotContain("secrets.json", names);
        }
        finally
        {
            if (Directory.Exists(home)) Directory.Delete(home, recursive: true);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true);
        }
    }
}

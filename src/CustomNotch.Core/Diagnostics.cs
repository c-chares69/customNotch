using System.IO.Compression;
using System.Text;

namespace CustomNotch.Core;

/// <summary>
/// Rapport de diagnostic : une archive à joindre quand quelque chose ne va pas. Journaux,
/// configuration et informations système. Contrairement à ClickUp-Extended, <c>config.json</c>
/// et <c>cells.json</c> ne portent jamais de secret (les jetons vivent dans <c>secrets.json</c>,
/// chiffré et jamais synchronisé, ou en placeholder <c>${secret:…}</c> dans <c>cells.json</c>) :
/// pas de caviardage à faire, <c>secrets.json</c> n'entre simplement jamais dans l'archive.
/// Reprise de ClickUp-Extended (<c>Diagnostics.cs</c>).
/// </summary>
public static class Diagnostics
{
    public static string SystemInfo(string home, IReadOnlyDictionary<string, string>? extra = null)
    {
        var lines = new List<string>
        {
            $"{App.Name} {App.Version}",
            $"Date : {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"Exécutable : {Environment.ProcessPath} (dossier {AppContext.BaseDirectory})",
            $".NET : {Environment.Version}",
            $"Système : {Environment.OSVersion} {(Environment.Is64BitOperatingSystem ? "x64" : "x86")}",
            $"Dossier de données : {home}",
            $"Culture : {System.Globalization.CultureInfo.CurrentCulture.Name}",
        };
        foreach (var (key, value) in extra ?? new Dictionary<string, string>()) lines.Add($"{key} : {value}");
        return string.Join("\n", lines) + "\n";
    }

    /// <summary>Noms et tailles (pas le contenu) des fichiers du dossier de données - de quoi voir d'un
    /// coup d'œil ce qui s'y trouve sans rien y copier de sensible.</summary>
    private static string ListFiles(string home)
    {
        if (!Directory.Exists(home)) return "(dossier de données absent)\n";
        var lines = Directory.EnumerateFiles(home, "*", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Select(p => $"{Path.GetRelativePath(home, p)} ({new FileInfo(p).Length} o)");
        return string.Join("\n", lines) + "\n";
    }

    /// <summary>Écrit <c>customNotch-diagnostic-&lt;horodatage&gt;.zip</c> sur le Bureau et retourne son chemin.</summary>
    public static string MakeReport(string? home = null, string? outDir = null, IReadOnlyDictionary<string, string>? extra = null)
    {
        home ??= Paths.Home();
        outDir ??= Paths.DesktopDir();
        Directory.CreateDirectory(outDir);
        var target = Path.Combine(outDir, $"{App.Name}-diagnostic-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        var utf8 = new UTF8Encoding(false);
        using (var archive = ZipFile.Open(target, ZipArchiveMode.Create))
        {
            void Put(string name, string content)
            {
                using var stream = archive.CreateEntry(name, CompressionLevel.Optimal).Open();
                var bytes = utf8.GetBytes(content);
                stream.Write(bytes, 0, bytes.Length);
            }
            Put("info.txt", SystemInfo(home, extra));
            var logs = Paths.LogDir(home);
            if (Directory.Exists(logs))
                foreach (var pattern in new[] { "journal.log*", "errors.log*" })
                    foreach (var path in Directory.GetFiles(logs, pattern).OrderBy(p => p, StringComparer.Ordinal))
                        archive.CreateEntryFromFile(path, Path.GetFileName(path), CompressionLevel.Optimal);
            // Jamais secrets.json : les tokens et mots de passe ne quittent pas la machine.
            foreach (var path in new[]
                     {
                         Paths.ConfigFile(home), Paths.DefaultCellsFile(home), Paths.LocalCellsFile(home),
                         Path.Combine(home, "claude-backoff.json"),
                     })
                if (File.Exists(path)) archive.CreateEntryFromFile(path, Path.GetFileName(path), CompressionLevel.Optimal);
            Put("fichiers.txt", ListFiles(home));
        }
        return target;
    }
}

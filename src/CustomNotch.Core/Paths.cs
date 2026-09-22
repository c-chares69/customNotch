namespace CustomNotch.Core;

/// <summary>Où vivent les données : la variable CUSTOMNOTCH_HOME, le mode portable (dossier data + fichier
/// vide portable à côté de l'exe), puis %APPDATA%\customNotch. Le fichier cells.json, lui, est où l'utilisateur
/// le décide (config.json : cells_path) - typiquement un dossier synchronisé entre machines.</summary>
public static class Paths
{
    public static string? PortableHome(string? exeDir = null)
    {
        var dir = exeDir ?? AppContext.BaseDirectory;
        var candidate = Path.Combine(dir, "data");
        return Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "portable")) ? candidate : null;
    }

    public static string Home()
    {
        var overrideDir = Environment.GetEnvironmentVariable(App.HomeEnv);
        if (!string.IsNullOrWhiteSpace(overrideDir)) return Path.GetFullPath(overrideDir);
        if (PortableHome() is { } portable) return portable;
        var appData = Environment.GetEnvironmentVariable("APPDATA");
        if (string.IsNullOrWhiteSpace(appData))
            appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Roaming");
        return Path.Combine(appData, App.Name);
    }

    public static string ConfigFile(string? home = null) => Path.Combine(home ?? Home(), "config.json");
    public static string DefaultCellsFile(string? home = null) => Path.Combine(home ?? Home(), "cells.json");
    /// <summary>La surcharge locale : positions, écran, visibilité - jamais synchronisée.</summary>
    public static string LocalCellsFile(string? home = null) => Path.Combine(home ?? Home(), $"cells.{MachineSlug()}.json");
    public static string SecretsFile(string? home = null) => Path.Combine(home ?? Home(), "secrets.json");
    public static string LogDir(string? home = null) => Path.Combine(home ?? Home(), "logs");

    /// <summary>Le nom de machine, en minuscules et sans caractère interdit dans un nom de fichier.</summary>
    public static string MachineSlug(string? machine = null)
    {
        var name = (machine ?? Environment.MachineName).ToLowerInvariant();
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) || c == '.' ? '-' : c).ToArray();
        var slug = new string(chars).Trim('-');
        return slug.Length == 0 ? "machine" : slug;
    }
}

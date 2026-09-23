using System.Diagnostics;
using Microsoft.Win32;

namespace CustomNotch.Core.Platform;

public static class Autostart
{
    public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static readonly string TaskName = App.Name;
    public static readonly string[] LegacyNames = Array.Empty<string>();

    /// <summary>La commande silencieuse : l'exécutable publié, ou `dotnet <dll>` en développement.</summary>
    public static string LaunchCommand()
    {
        var process = Environment.ProcessPath ?? "";
        if (process.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            // En développement seulement : publié en exécutable unique, l'assembly n'a pas de chemin.
            var name = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name ?? App.Name;
            return $"\"{process}\" \"{Path.Combine(AppContext.BaseDirectory, name + ".dll")}\"";
        }
        return $"\"{process}\"";
    }

    private static bool Schtasks(params string[] args)
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            var info = new ProcessStartInfo("schtasks") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var a in args) info.ArgumentList.Add(a);
            using var p = Process.Start(info)!;
            p.WaitForExit(10_000);
            return p.ExitCode == 0;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }

    public static bool TaskInstalled() => Schtasks("/Query", "/TN", TaskName);
    public static bool RemoveTask() => Schtasks("/Delete", "/TN", TaskName, "/F");

    private static string? StoredCommand()
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(App.Name) as string;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static bool RunKeyEnabled() => StoredCommand() is not null;

    /// <summary>Démarre-t-elle avec Windows, par la clé Run ou par la tâche planifiée ?</summary>
    public static bool IsEnabled() => RunKeyEnabled() || TaskInstalled();

    /// <summary>La commande enregistrée pointe-t-elle encore vers cette copie ?</summary>
    public static bool IsStale()
    {
        var stored = StoredCommand();
        if (stored is null) return false;
        if (stored.Trim() == LaunchCommand()) return false;
        return !TaskInstalled();
    }

    public static void ForgetLegacyNames()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            foreach (var name in LegacyNames) key?.DeleteValue(name, throwOnMissingValue: false);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Retourne true si l'opération a réussi.</summary>
    public static bool SetEnabled(bool enabled)
    {
        if (!OperatingSystem.IsWindows()) return false;
        ForgetLegacyNames();
        var task = TaskInstalled();
        if (!enabled && task) RemoveTask();
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (key is null) return false;
            if (enabled && !task) key.SetValue(App.Name, LaunchCommand(), RegistryValueKind.String);
            else key.DeleteValue(App.Name, throwOnMissingValue: false);
            return true;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

using System.Diagnostics;
using Microsoft.Win32;

namespace CustomNotch.Core.Platform;

/// <summary>Démarrage automatique : les mêmes deux tâches planifiées que l'installateur (customNotch à l'ouverture de
/// session, customNotch Watchdog toutes les 15 min), pour que la case des réglages et l'installation disent la même
/// chose — décocher la case doit aussi arrêter le watchdog, sans quoi il relance l'application. La clé Run d'une
/// ancienne version n'est plus un second mode de démarrage : si elle traîne encore, SetEnabled la retire dans les
/// deux sens plutôt que de laisser cohabiter deux mécanismes.</summary>
public static class Autostart
{
    public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static readonly string TaskName = App.Name;
    public static readonly string WatchdogName = App.Name + " Watchdog";

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

    /// <summary>Lance un script posé à côté de l'exécutable (cas installé) fenêtre cachée, attend au plus 30 s.</summary>
    private static bool RunScript(string scriptPath, params string[] args)
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            var info = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true };
            info.ArgumentList.Add("-NoProfile");
            info.ArgumentList.Add("-ExecutionPolicy");
            info.ArgumentList.Add("Bypass");
            info.ArgumentList.Add("-File");
            info.ArgumentList.Add(scriptPath);
            foreach (var a in args) info.ArgumentList.Add(a);
            using var p = Process.Start(info)!;
            return p.WaitForExit(30_000) && p.ExitCode == 0;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }

    public static bool TaskInstalled() => Schtasks("/Query", "/TN", TaskName);
    public static bool RemoveTask() => Schtasks("/Delete", "/TN", TaskName, "/F");

    /// <summary>Démarre-t-elle avec Windows ? Seule la tâche de session compte : la clé Run n'est plus un mode.</summary>
    public static bool IsEnabled() => TaskInstalled();

    private static bool RemoveRunKey()
    {
        if (!OperatingSystem.IsWindows()) return true;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            key?.DeleteValue(App.Name, throwOnMissingValue: false);
            return true;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Pose les deux tâches (script d'installation trouvé à côté de l'exécutable) ou, depuis les sources,
    /// les crée directement par schtasks — le même triplet TN/TR/SC que l'installateur.</summary>
    private static bool Enable()
    {
        var script = Path.Combine(AppContext.BaseDirectory, "install_tasks.ps1");
        if (File.Exists(script)) return RunScript(script, "-Exe", Environment.ProcessPath ?? "");
        var session = Schtasks("/Create", "/TN", TaskName, "/TR", LaunchCommand(), "/SC", "ONLOGON", "/RL", "LIMITED", "/F");
        var watchdog = Schtasks("/Create", "/TN", WatchdogName, "/TR", LaunchCommand() + " --auto", "/SC", "MINUTE", "/MO", "15", "/F");
        return session && watchdog;
    }

    /// <summary>Retire les deux tâches (script de retrait trouvé à côté de l'exécutable, qui garde l'entrée
    /// « Applications installées ») ou, depuis les sources, par schtasks directement.</summary>
    private static bool Disable()
    {
        var script = Path.Combine(AppContext.BaseDirectory, "remove_tasks.ps1");
        if (File.Exists(script)) return RunScript(script, "-KeepRegistry");
        var session = Schtasks("/Delete", "/TN", TaskName, "/F");
        var watchdog = Schtasks("/Delete", "/TN", WatchdogName, "/F");
        return session && watchdog;
    }

    /// <summary>Retourne true si l'opération a réussi.</summary>
    public static bool SetEnabled(bool enabled)
    {
        if (!OperatingSystem.IsWindows()) return false;
        var ok = enabled ? Enable() : Disable();
        RemoveRunKey();
        return ok;
    }
}

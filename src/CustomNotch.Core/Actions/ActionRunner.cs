using System.Diagnostics;
using CustomNotch.Core.Config;

namespace CustomNotch.Core.Actions;

/// <summary>Les trois actions d'une cellule : open (URL, fichier, app), shell (commande, sans fenêtre), source
/// (méthode de la source, déléguée à l'ordonnanceur).</summary>
public static class ActionRunner
{
    public static bool Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            Log.Warning("action", $"open « {target} » : {ex.Message}");
            return false;
        }
    }

    /// <summary>cmd.exe /c, fenêtre cachée, sans attendre : une action ne bloque jamais la pilule.</summary>
    public static bool Shell(string command)
    {
        try
        {
            // Arguments (ligne brute), pas ArgumentList : cmd.exe ne suit pas l'échappement CRT standard des
            // arguments et double les guillemets internes (ex. un chemin entre guillemets, un JSON échoué).
            var info = new ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                Arguments = $"/c {command}",
            };
            Process.Start(info);
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            Log.Warning("action", $"shell « {command} » : {ex.Message}");
            return false;
        }
    }

    public static async Task RunAsync(ActionConfig action, Func<string, Task> sourceAction)
    {
        if (!string.IsNullOrWhiteSpace(action.Open)) Open(action.Open);
        else if (!string.IsNullOrWhiteSpace(action.Shell)) Shell(action.Shell);
        else if (!string.IsNullOrWhiteSpace(action.Source)) await sourceAction(action.Source).ConfigureAwait(false);
    }
}

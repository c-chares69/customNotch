using System.Diagnostics;

namespace CustomNotch.Core.Actions;

public static class ActionRunner
{
    /// <summary>Ouvre une URL, un fichier, un dossier ou une application par le shell (comme un double-clic).</summary>
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
}

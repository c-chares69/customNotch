using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CustomNotch.Core;

/// <summary>Écriture des fichiers JSON de l'application : même mise en forme partout,
/// et un remplacement atomique qui insiste quand le fichier est verrouillé.</summary>
public static class Json
{
    /// <summary>Indenté, accents et guillemets en clair - lisible dans un éditeur.</summary>
    public static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string Format(JsonNode node) => node.ToJsonString(Pretty);

    /// <summary>Écrit dans un fichier temporaire puis remplace la cible d'un coup ; sans BOM,
    /// pour rester lisible par tout lecteur JSON strict. Retourne false, après journal, si le
    /// fichier reste verrouillé.</summary>
    public static bool WriteAtomic(string path, string text, string category, int attempts = 6)
    {
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        var tmp = Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(path)}.{Environment.ProcessId}.{Environment.CurrentManagedThreadId}.tmp");
        try { File.WriteAllText(tmp, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)); }
        catch (IOException ex) { Log.Warning(category, $"Fichier non écrit ({Path.GetFileName(tmp)}) : {ex.Message}"); return false; }
        catch (UnauthorizedAccessException ex) { Log.Warning(category, $"Fichier non écrit ({Path.GetFileName(tmp)}) : {ex.Message}"); return false; }
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try { File.Move(tmp, path, overwrite: true); return true; }
            catch (IOException) { Thread.Sleep(50 * (attempt + 1)); }
            catch (UnauthorizedAccessException) { Thread.Sleep(50 * (attempt + 1)); }
        }
        try { File.Delete(tmp); } catch (IOException) { }
        Log.Warning(category, $"Fichier non écrit : {Path.GetFileName(path)} reste verrouillé");
        return false;
    }
}

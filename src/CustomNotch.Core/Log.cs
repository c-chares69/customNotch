using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace CustomNotch.Core;

/// <summary>
/// Le journal : <c>logs/journal.log</c> pour tout, <c>logs/errors.log</c> pour ce qui
/// mérite un regard. Les tokens et jetons sont caviardés avant d'être écrits.
/// </summary>
public static partial class Log
{
    private static readonly object Lock = new();
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private static string? _dir;

    /// <summary>Les valeurs de secrets résolus (${secret:…}) à caviarder littéralement : les motifs Bearer/pk_/sk-/
    /// ya29 de <see cref="Token"/> ne couvrent que des formes connues, alors qu'un secret résolu peut apparaître
    /// tel quel (target d'un « open », commande d'un « shell ») sans porter aucun de ces préfixes.</summary>
    private static readonly ConcurrentDictionary<string, byte> Secrets = new();

    /// <summary>Le dossier des journaux ; null = pas d'écriture sur le disque (tests).</summary>
    public static string? Directory
    {
        get => _dir;
        set => _dir = value;
    }

    public static event Action<string, string, string>? Written;

    [GeneratedRegex(@"(Bearer\s+|pk_|sk-|ya29\.)([A-Za-z0-9_\-\.]{6})[A-Za-z0-9_\-\.]+")]
    private static partial Regex Token();

    /// <summary>Enregistre des valeurs de secrets à masquer dans tout ce qui est journalisé ensuite (appelé après
    /// chaque résolution de config). Les valeurs trop courtes (&lt; 4 caractères) sont ignorées : les masquer
    /// caviarderait des bouts de texte ordinaires sans protéger grand-chose.</summary>
    public static void Mask(IEnumerable<string> secrets)
    {
        foreach (var s in secrets)
            if (!string.IsNullOrEmpty(s) && s.Length >= 4)
                Secrets[s] = 0;
    }

    public static string Redact(string text)
    {
        foreach (var secret in Secrets.Keys)
            text = text.Replace(secret, "…");
        return Token().Replace(text, "$1$2…");
    }

    public static void Info(string category, string message) => Write("INFO", category, message);
    public static void Warning(string category, string message) => Write("WARN", category, message);
    public static void Error(string category, string message) => Write("ERROR", category, message);

    private static void Write(string level, string category, string message)
    {
        var safe = Redact(message);
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {level,-5} [{category}] {safe}";
        Written?.Invoke(level, category, safe);
        if (_dir is null) return;
        lock (Lock)
        {
            try
            {
                System.IO.Directory.CreateDirectory(_dir);
                File.AppendAllText(Path.Combine(_dir, "journal.log"), line + Environment.NewLine, Utf8NoBom);
                if (level != "INFO")
                    File.AppendAllText(Path.Combine(_dir, "errors.log"), line + Environment.NewLine, Utf8NoBom);
            }
            catch (IOException)
            {
                // Un journal qui refuse d'écrire ne doit jamais faire tomber l'application.
            }
        }
    }
}

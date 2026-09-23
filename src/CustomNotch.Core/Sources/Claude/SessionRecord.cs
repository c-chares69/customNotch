using System.Text.Json.Nodes;

namespace CustomNotch.Core.Sources.Claude;

/// <summary>Où en est une session : <c>busy</c>/<c>waiting</c> viennent du fichier, <c>Idle</c> couvre tout le
/// reste (y compris un <c>status</c> absent), <c>Ended</c> n'existe que côté <see cref="ClaudeSession"/> — une
/// session terminée n'a plus de fichier à lire.</summary>
public enum SessionState { Busy, Waiting, Idle, Ended }

/// <summary>Le contenu brut d'un <c>~/.claude/sessions/&lt;pid&gt;.json</c>, tel qu'écrit par Claude Code
/// lui-même : seuls <see cref="Pid"/> et <see cref="Cwd"/> sont garantis - tout le reste (identité de session,
/// horodatages, surface) peut manquer selon la version du CLI, et <see cref="Parse"/> le tolère.</summary>
public sealed record SessionRecord(int Pid, string Cwd, string Name, string RawStatus, string? SessionId,
    long? StartedAtMs, long? UpdatedAtMs, string? Entrypoint, long? ProcStartFileTime)
{
    /// <summary>Tolérant : rend <c>null</c> seulement si <c>pid</c> ou <c>cwd</c> manque - un fichier à moitié
    /// écrit (Claude Code en train de le créer) ne doit jamais faire tomber <see cref="SessionRegistry.Scan"/>.
    /// <c>name</c> absent retombe sur le dernier segment de <c>cwd</c> (comme le nom d'un dossier de projet).</summary>
    public static SessionRecord? Parse(JsonNode root)
    {
        if (root is not JsonObject o) return null;
        if (!TryInt(o["pid"], out var pid)) return null;
        var cwd = ReadString(o["cwd"]);
        if (string.IsNullOrEmpty(cwd)) return null;

        var name = ReadString(o["name"]);
        if (string.IsNullOrEmpty(name)) name = LastSegment(cwd);

        var status = ReadString(o["status"]) ?? "";
        var sessionId = ReadString(o["sessionId"]);
        var entrypoint = ReadString(o["entrypoint"]);
        var startedAtMs = TryLong(o["startedAt"], out var startedAt) ? startedAt : (long?)null;
        var updatedAtMs = TryLong(o["updatedAt"], out var updatedAt) ? updatedAt : (long?)null;
        // procStart est une FILETIME Windows (100 ns) écrite en texte pour ne pas perdre de précision en JSON
        // (un double ne tient pas 64 bits entiers exacts) : on la reparse en long, jamais en nombre JSON.
        var procStartFileTime = ReadString(o["procStart"]) is { } raw && long.TryParse(raw, out var pf) ? pf : (long?)null;

        return new SessionRecord(pid, cwd, name, status, sessionId, startedAtMs, updatedAtMs, entrypoint, procStartFileTime);
    }

    private static string LastSegment(string cwd)
    {
        var trimmed = cwd.TrimEnd('/', '\\');
        var i = trimmed.LastIndexOfAny(new[] { '/', '\\' });
        return i >= 0 && i + 1 < trimmed.Length ? trimmed[(i + 1)..] : trimmed;
    }

    private static string? ReadString(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static bool TryInt(JsonNode? node, out int value)
    {
        if (node is JsonValue v)
        {
            if (v.TryGetValue<int>(out value)) return true;
            if (v.TryGetValue<double>(out var d)) { value = (int)d; return true; }
        }
        value = 0;
        return false;
    }

    private static bool TryLong(JsonNode? node, out long value)
    {
        if (node is JsonValue v)
        {
            if (v.TryGetValue<long>(out value)) return true;
            if (v.TryGetValue<double>(out var d)) { value = (long)d; return true; }
        }
        value = 0;
        return false;
    }
}

/// <summary>Une session Claude Code telle que montrée par la cellule : dédoublonnée, sa vivacité vérifiée, prête
/// pour l'affichage. <see cref="SinceMs"/> = dernière mise à jour connue pour une session vivante, instant de
/// disparition pour une session <see cref="SessionState.Ended"/>.</summary>
public sealed record ClaudeSession(string Id, string Name, SessionState State, long SinceMs, string Surface, int Pid);

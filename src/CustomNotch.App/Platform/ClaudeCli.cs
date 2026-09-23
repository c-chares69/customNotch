using System.Diagnostics;
using System.IO;
using CustomNotch.Core;

namespace CustomNotch.App.Platform;

/// <summary>Ce que le cœur (<c>TokenRenewal</c>, <c>ClaudeSource.SignIn</c>) ne peut pas faire lui-même : trouver
/// le CLI <c>claude</c> sur ce poste, le lancer caché pour renouveler le jeton, ouvrir un terminal visible pour la
/// connexion, et lire l'heure de démarrage d'un processus (vivacité d'une session, <see cref="SessionRegistry"/>
/// côté cœur). Rien ici n'est spécifique à Claude Code au sens métier - juste du <c>Process</c> Windows.</summary>
public static class ClaudeCli
{
    private static readonly string[] PathNames = { "claude.exe", "claude.cmd", "claude" };

    /// <summary>Le PATH d'abord (trois noms possibles, un CLI à jour l'y pose), puis les emplacements connus des
    /// installateurs courants : WinGet (<c>Anthropic.ClaudeCode</c>), l'installateur natif Claude Code
    /// (<c>~/.local/bin</c>), npm global. <c>null</c> si rien n'existe - jamais une exception.</summary>
    public static string? Find()
    {
        foreach (var dir in PathDirectories())
            foreach (var name in PathNames)
            {
                var candidate = Path.Combine(dir, name);
                if (File.Exists(candidate)) return candidate;
            }
        foreach (var fallback in FallbackPaths())
            if (File.Exists(fallback)) return fallback;
        return null;
    }

    private static IEnumerable<string> PathDirectories()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>Sur ce poste, WinGet pose <c>claude.exe</c> ici ; sans PATH à jour dans le processus (un
    /// service, une tâche planifiée démarrée avant l'installation), il faut le trouver quand même.</summary>
    private static IEnumerable<string> FallbackPaths()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(localAppData, "Microsoft", "WinGet", "Links", "claude.exe");

        var userProfile = Environment.GetEnvironmentVariable("USERPROFILE") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return Path.Combine(userProfile, ".local", "bin", "claude.exe");

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        yield return Path.Combine(appData, "npm", "claude.cmd");
    }

    private static bool IsScript(string cli) => cli.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) || cli.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);

    /// <summary>Renouvellement du jeton : <c>claude -p</c>, fenêtre cachée, stdin fermé tout de suite (jamais
    /// d'attente sur une entrée qui ne viendra pas), stdout/stderr drainés en continu (même patron que
    /// <c>ShellSource</c> - un tube plein bloquerait l'enfant), tué à l'échéance. <paramref name="launched"/>
    /// reçoit le pid dès le lancement, pour que <c>SessionRegistry</c> l'ignore (ce n'est pas une session de
    /// travail).</summary>
    public static async Task<int> RunHiddenAsync(string cli, Action<int>? launched, TimeSpan timeout)
    {
        var info = IsScript(cli)
            ? new ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe") { Arguments = $"/c \"{cli}\" -p" }
            : new ProcessStartInfo(cli) { ArgumentList = { "-p" } };
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        info.RedirectStandardInput = true;
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;

        using var process = Process.Start(info) ?? throw new InvalidOperationException("impossible de lancer claude");
        process.StandardInput.Close();
        launched?.Invoke(process.Id);

        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(timeout);
        try { await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
        }
        try { await Task.WhenAll(stdout, stderr).ConfigureAwait(false); }
        catch (Exception) { /* on ne fait que drainer les tubes, une erreur de lecture ne doit rien changer au résultat */ }
        return process.HasExited ? process.ExitCode : -1;
    }

    /// <summary>Connexion : un terminal visible sur <c>claude auth login --claudeai</c> (l'utilisateur doit voir
    /// l'URL et l'interaction), comme <c>ActionRunner.Shell</c> mais sans fenêtre cachée. Arguments en ligne brute
    /// (pas <c>ArgumentList</c>) : <c>start</c> a besoin d'un titre vide entre guillemets avant la cible, une
    /// convention que cmd.exe seul comprend.</summary>
    public static void SignIn(string cli)
    {
        try
        {
            var info = new ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe")
            {
                UseShellExecute = false,
                Arguments = $"/c start \"\" \"{cli}\" auth login --claudeai",
            };
            Process.Start(info);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            Log.Warning("claude", $"connexion : {ex.Message}");
        }
    }

    /// <summary>L'heure de démarrage d'un processus vivant, en FILETIME (unité de <c>SessionRecord.ProcStart</c>) -
    /// <c>null</c> s'il n'existe plus, ou si son propriétaire n'est pas ce compte Windows (accès refusé) : dans les
    /// deux cas, <c>SessionRegistry</c> doit simplement conclure que la session n'est pas vivante, jamais planter.</summary>
    public static long? ProcessStartFileTime(int pid)
    {
        try { return Process.GetProcessById(pid).StartTime.ToFileTime(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return null;
        }
    }
}

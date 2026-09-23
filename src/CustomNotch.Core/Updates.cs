using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CustomNotch.Core.Config;

namespace CustomNotch.Core;

/// <summary>Manifeste illisible, réseau, empreinte fausse : remonté tel quel à l'interface.</summary>
public sealed class UpdateException : Exception
{
    public UpdateException(string message, Exception? inner = null) : base(message, inner) { }
}

public sealed record Release(string Version, string Url, string Sha256 = "", string Notes = "", string Published = "")
{
    public bool Installable => Sha256.Length > 0 && Uri.TryCreate(Url, UriKind.Absolute, out var u) && u.Scheme is "http" or "https";
}

/// <summary>
/// Mises à jour : un manifeste distant <c>latest.json</c>, un téléchargement vérifié par
/// SHA-256, une réinstallation silencieuse. Même manifeste que la version Python.
/// </summary>
public static partial class Updates
{
    public const string ManifestName = "latest.json";
    public static readonly string[] SilentSetupArgs = { "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/CLOSEAPPLICATIONS" };

    /// <summary>Le dépôt public de l'application : utilisée quand <c>updates.url</c> est vide plutôt que de
    /// refuser de vérifier - <c>latest.json</c> est déjà joint à chaque release GitHub.</summary>
    public const string DefaultManifestUrl = "https://github.com/c-chares69/customNotch/releases/latest/download/latest.json";

    /// <summary>Le client HTTP - remplaçable dans les tests.</summary>
    public static HttpClient Http { get; set; } = new() { Timeout = TimeSpan.FromSeconds(15) };

    [GeneratedRegex(@"\d+")]
    private static partial Regex Digits();
    [GeneratedRegex(@"^[0-9a-f]{64}$")]
    private static partial Regex Sha256Pattern();

    public static (int, int, int) ParseVersion(string? text)
    {
        var parts = Digits().Matches(text ?? "").Select(m => int.Parse(m.Value)).Take(3).ToList();
        while (parts.Count < 3) parts.Add(0);
        return (parts[0], parts[1], parts[2]);
    }

    public static bool IsNewer(string candidate, string? current = null)
        => ParseVersion(candidate).CompareTo(ParseVersion(current ?? App.Version)) > 0;

    /// <summary>Valide le manifeste ; une URL relative est résolue contre celle du manifeste.</summary>
    public static Release ParseManifest(JsonNode? raw, string baseUrl = "")
    {
        if (raw is not JsonObject obj) throw new UpdateException("Manifeste illisible : un objet JSON était attendu.");
        var version = (obj["version"]?.ToString() ?? "").Trim();
        var url = (obj["url"]?.ToString() ?? "").Trim();
        if (version.Length == 0 || !Digits().IsMatch(version)) throw new UpdateException("Manifeste sans numéro de version.");
        if (url.Length == 0) throw new UpdateException("Manifeste sans adresse de téléchargement.");
        if (baseUrl.Length > 0 && Uri.TryCreate(new Uri(baseUrl), url, out var resolved)) url = resolved.ToString();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) || parsed.Scheme is not ("http" or "https"))
            throw new UpdateException("Adresse de téléchargement refusée : http(s) seulement.");
        var sha = (obj["sha256"]?.ToString() ?? "").Trim().ToLowerInvariant();
        if (sha.Length > 0 && !Sha256Pattern().IsMatch(sha)) throw new UpdateException("Empreinte SHA-256 mal formée dans le manifeste.");
        return new Release(version, url, sha, obj["notes"]?.ToString() ?? "", obj["published"]?.ToString() ?? "");
    }

    public static async Task<Release> FetchManifestAsync(string url, CancellationToken ct = default)
    {
        HttpResponseMessage resp;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new UpdateException("Adresse du manifeste invalide : il faut une adresse http(s) complète.");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("User-Agent", $"{App.Name}/{App.Version}");
            resp = await Http.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new UpdateException($"Réseau indisponible ({ex.GetType().Name}).", ex);
        }
        using (resp)
        {
            if ((int)resp.StatusCode >= 400) throw new UpdateException($"Manifeste introuvable (HTTP {(int)resp.StatusCode}).");
            var text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            JsonNode? raw;
            try { raw = JsonNode.Parse(text); }
            catch (JsonException ex) { throw new UpdateException("Manifeste illisible (JSON attendu).", ex); }
            return ParseManifest(raw, url);
        }
    }

    /// <summary>Télécharge et vérifie l'empreinte avant de rendre le fichier.</summary>
    public static async Task<string> DownloadAsync(Release release, string destDir, Action<string>? progress = null, CancellationToken ct = default)
    {
        if (!release.Installable) throw new UpdateException("Cette version ne porte pas d'empreinte : installation manuelle.");
        Directory.CreateDirectory(destDir);
        var name = Path.GetFileName(new Uri(release.Url).AbsolutePath);
        var target = Path.Combine(destDir, name.Length > 0 ? name : "mise-a-jour.zip");
        try
        {
            using var resp = await Http.GetAsync(release.Url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if ((int)resp.StatusCode >= 400) throw new UpdateException($"Téléchargement refusé (HTTP {(int)resp.StatusCode}).");
            var total = resp.Content.Headers.ContentLength ?? 0;
            long done = 0;
            using var sha = SHA256.Create();
            await using (var input = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            await using (var output = File.Create(target))
            {
                var buffer = new byte[256 * 1024];
                int read;
                while ((read = await input.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    sha.TransformBlock(buffer, 0, read, null, 0);
                    done += read;
                    if (progress is not null && total > 0) progress($"Téléchargement {done * 100 / total} %");
                }
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            }
            var digest = Convert.ToHexString(sha.Hash!).ToLowerInvariant();
            if (digest != release.Sha256)
            {
                File.Delete(target);
                throw new UpdateException("Empreinte du fichier téléchargé différente de celle annoncée : mise à jour refusée.");
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new UpdateException($"Téléchargement interrompu ({ex.GetType().Name}).", ex);
        }
        return target;
    }

    /// <summary>Décompresse et retourne le dossier qui contient <c>install.ps1</c> et l'application.</summary>
    public static string Extract(string archive, string destDir)
    {
        Directory.CreateDirectory(destDir);
        var root = Path.GetFullPath(destDir);
        try
        {
            using var bundle = ZipFile.OpenRead(archive);
            foreach (var entry in bundle.Entries)
            {
                // Un zip hostile peut viser l'extérieur du dossier (« zip slip »).
                var target = Path.GetFullPath(Path.Combine(root, entry.FullName));
                if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && target != root)
                    throw new UpdateException("Archive refusée : chemin sortant du dossier.");
            }
            bundle.ExtractToDirectory(root, overwriteFiles: true);
        }
        catch (InvalidDataException ex)
        {
            throw new UpdateException("Archive illisible.", ex);
        }
        foreach (var candidate in new[] { root }.Concat(Directory.GetDirectories(root).OrderBy(d => d)))
        {
            if (File.Exists(Path.Combine(candidate, "install.ps1")) && File.Exists(Path.Combine(candidate, App.Name, App.Name + ".exe")))
                return candidate;
        }
        throw new UpdateException("Archive incomplète : install.ps1 ou l'application manquent.");
    }

    /// <summary>La commande qui installe : un setup.exe silencieux, ou le install.ps1 d'une archive.</summary>
    public static List<string> InstallerCommand(string stage)
    {
        if (File.Exists(stage) && Path.GetExtension(stage).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            return new List<string> { stage }.Concat(SilentSetupArgs).ToList();
        return new List<string>
        {
            "powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(stage, "install.ps1"),
            "-Source", Path.Combine(stage, App.Name),
        };
    }

    /// <summary>Lance l'installateur, détaché : il arrête et remplace l'application, puis la redémarre.</summary>
    public static void LaunchInstaller(string stage)
    {
        if (!OperatingSystem.IsWindows()) throw new UpdateException("La réinstallation automatique n'existe que sous Windows.");
        var command = InstallerCommand(stage);
        var info = new System.Diagnostics.ProcessStartInfo(command[0])
        {
            UseShellExecute = false, CreateNoWindow = true,
            WorkingDirectory = File.Exists(stage) ? Path.GetDirectoryName(stage)! : stage,
        };
        foreach (var arg in command.Skip(1)) info.ArgumentList.Add(arg);
        System.Diagnostics.Process.Start(info);
    }
}

/// <summary>Vérifie, télécharge et prépare une mise à jour hors du thread d'interface.</summary>
public sealed class UpdateChecker
{
    public event Action<Release>? Available;
    public event Action<string>? UpToDate;
    public event Action<string, bool>? Failed;
    public event Action<string>? Progress;
    public event Action<string>? ReadyToInstall;

    private readonly AppConfig _cfg;
    public Release? Latest { get; private set; }
    public bool Busy { get; private set; }

    public UpdateChecker(AppConfig cfg) => _cfg = cfg;

    private static double UnixNow() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>Publié en exécutable sous Windows : l'installateur peut nous remplacer.</summary>
    public static bool CanSelfInstall => OperatingSystem.IsWindows()
        && !(Environment.ProcessPath ?? "").EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase);

    /// <summary>Faut-il vérifier maintenant, d'après la cadence et la dernière vérification ? Une adresse de
    /// manifeste vide ne désactive plus rien : <see cref="Updates.DefaultManifestUrl"/> prend le relais.</summary>
    public bool Due()
    {
        if (!_cfg.GetBool("updates.enabled", true)) return false;
        var hours = _cfg.GetDouble("updates.interval_hours", 24);
        if (hours <= 0) hours = 24;
        return UnixNow() - _cfg.GetDouble("updates.last_check") >= hours * 3600;
    }

    public bool Check(bool manual = false)
    {
        var url = _cfg.GetString("updates.url").Trim();
        if (url.Length == 0) url = Updates.DefaultManifestUrl;
        if (Busy) return false;
        Busy = true;
        _ = Task.Run(async () =>
        {
            Release release;
            try
            {
                release = await Updates.FetchManifestAsync(url).ConfigureAwait(false);
            }
            catch (UpdateException ex)
            {
                Busy = false;
                _cfg.Set("updates.last_check", UnixNow());
                _cfg.Save();
                Log.Info("maj", $"Vérification impossible : {ex.Message}");
                Failed?.Invoke(ex.Message, manual);
                return;
            }
            Busy = false;
            _cfg.Set("updates.last_check", UnixNow());
            _cfg.Save();
            if (!Updates.IsNewer(release.Version))
            {
                Latest = null;
                UpToDate?.Invoke(release.Version);
                return;
            }
            Latest = release;
            if (!manual && _cfg.GetString("updates.skipped_version") == release.Version) return;
            Available?.Invoke(release);
        });
        return true;
    }

    public void Skip(string version)
    {
        _cfg.Set("updates.skipped_version", version);
        _cfg.Save();
    }

    /// <summary>Télécharge et décompresse ; <see cref="ReadyToInstall"/> porte ce qu'il faut installer.</summary>
    public bool Prepare(Release release)
    {
        if (Busy) return false;
        Busy = true;
        _ = Task.Run(async () =>
        {
            try
            {
                var stage = Path.Combine(Path.GetTempPath(), "customnotch-maj-" + Guid.NewGuid().ToString("N")[..8]);
                var payload = await Updates.DownloadAsync(release, stage, p => Progress?.Invoke(p)).ConfigureAwait(false);
                string ready;
                if (Path.GetExtension(payload).Equals(".exe", StringComparison.OrdinalIgnoreCase)) ready = payload;
                else
                {
                    Progress?.Invoke("Vérification et décompression…");
                    ready = Updates.Extract(payload, Path.Combine(stage, "contenu"));
                }
                Busy = false;
                ReadyToInstall?.Invoke(ready);
            }
            catch (UpdateException ex)
            {
                Busy = false;
                Log.Warning("maj", $"Préparation refusée : {ex.Message}");
                Failed?.Invoke(ex.Message, true);
            }
        });
        return true;
    }
}

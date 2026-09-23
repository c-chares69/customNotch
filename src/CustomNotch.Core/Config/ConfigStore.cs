using System.Text.Json.Nodes;
using CustomNotch.Core.Sources;

namespace CustomNotch.Core.Config;

/// <summary>Les trois fichiers et leur cycle : cells.json (partagé, où l'utilisateur veut) + cells.<machine>.json (local)
/// + secrets.json → un CellsFile validé. Surveillance avec anti-rebond ; une version fausse est refusée et l'ancienne
/// reste en service. L'application n'écrit le fichier partagé que depuis Settings ; ce que l'app décide seule
/// (position après un drag, écran, visibilité) va dans la surcharge locale.</summary>
public sealed class ConfigStore : IDisposable
{
    private readonly string _home;
    private readonly IReadOnlyDictionary<string, SourceSchema> _schemas;
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly object _lock = new();
    private System.Threading.Timer? _debounce;
    private bool _disposed;
    /// <summary>Le texte JSON résolu (placeholders inclus) du dernier chargement réussi. Un ConfigEditor écrit le
    /// fichier partagé puis recharge tout de suite ; 300 ms plus tard, le FileSystemWatcher détecte ce même écrit
    /// et redéclenche un rechargement avec un contenu identique — sans ce garde-fou, chaque abonné (page Pilules,
    /// éditeur de cellule…) reconstruirait son UI pour rien à chaque frappe. Mis à jour seulement sur un
    /// chargement accepté : un refus entre deux n'y touche pas.</summary>
    private string? _lastResolvedJson;

    public event Action<CellsFile>? Changed;
    public event Action<string>? Rejected;

    public ConfigStore(string home, IReadOnlyDictionary<string, SourceSchema> schemas)
    {
        _home = home;
        _schemas = schemas;
        Directory.CreateDirectory(home);
        App = new AppConfig(Paths.ConfigFile(home));
        Secrets = new SecretsFile(Paths.SecretsFile(home));
        var custom = App.GetString("cells_path");
        CellsPath = custom.Length > 0 ? Path.GetFullPath(custom) : Paths.DefaultCellsFile(home);
        LocalPath = Paths.LocalCellsFile(home);
        Current = new CellsFile();
    }

    public AppConfig App { get; }
    public SecretsFile Secrets { get; }
    public string CellsPath { get; }
    public string LocalPath { get; }
    public CellsFile Current { get; private set; }
    public IReadOnlyList<string> LastErrors { get; private set; } = Array.Empty<string>();

    /// <summary>Lit, fusionne, résout, valide. Rend true si une nouvelle configuration est en service.</summary>
    public bool Load()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(CellsPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(CellsPath)!);
                    Json.WriteAtomic(CellsPath, DefaultCells.Json(), "config");
                    Log.Info("config", $"cells.json créé : {CellsPath}");
                }
                var shared = CellsJson.Parse(File.ReadAllText(CellsPath));
                var local = File.Exists(LocalPath) ? CellsJson.Parse(File.ReadAllText(LocalPath)) : null;
                var merged = ConfigMerge.Merge(shared, local);
                var (resolved, missing, secretValues) = Placeholders.Resolve(merged, Environment.GetEnvironmentVariable, Secrets, _home);
                foreach (var m in missing) Log.Warning("config", $"placeholder ${{{m}}} sans valeur");
                // Un secret résolu (jeton ClickUp, clé API…) peut finir dans une cible « open » ou une commande
                // « shell » journalisée telle quelle : on l'enregistre ici pour qu'il soit caviardé partout, pas
                // seulement dans les formes que Log.Redact reconnaît déjà (Bearer, pk_, sk-, ya29).
                Log.Mask(secretValues);
                var file = CellsJson.ToFile(resolved!.AsObject());
                var errors = ConfigValidation.Validate(file, _schemas);
                LastErrors = errors;
                if (errors.Count > 0)
                {
                    var message = $"{Path.GetFileName(CellsPath)} refusé : " + string.Join(" ; ", errors);
                    Log.Warning("config", message);
                    Rejected?.Invoke(message);
                    return false;
                }
                var text = resolved!.ToJsonString(CellsJson.Options);
                var unchanged = text == _lastResolvedJson;
                _lastResolvedJson = text;
                Current = file;
                if (!unchanged) Changed?.Invoke(file);
                return true;
            }
            catch (Exception ex)
            {
                // Ici est la frontière de la configuration : quelle que soit la façon dont un fichier malformé
                // ou un placeholder tordu fait tomber la lecture, on refuse en bloc, on journalise, et la
                // précédente config reste en service - jamais une exception qui remonte jusqu'au thread du
                // FileSystemWatcher ou du minuteur de rebond et tue le processus.
                LastErrors = new[] { ex.Message };
                Log.Warning("config", ex.Message);
                Rejected?.Invoke(ex.Message);
                return false;
            }
        }
    }

    public void StartWatching()
    {
        foreach (var path in new[] { CellsPath, LocalPath, Paths.SecretsFile(_home) }.Distinct())
        {
            var dir = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(dir);
            var w = new FileSystemWatcher(dir, Path.GetFileName(path)) { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size };
            w.Changed += (_, _) => Bump();
            w.Created += (_, _) => Bump();
            w.Renamed += (_, _) => Bump();
            w.EnableRaisingEvents = true;
            _watchers.Add(w);
        }
    }

    /// <summary>Un éditeur écrit en plusieurs fois : on attend 300 ms de calme avant de relire. Le verrou couvre
    /// aussi la vérification de <c>_disposed</c> : après Dispose(), plus aucun rechargement ne part, même si un
    /// événement du watcher ou une minuterie en vol arrive juste après.</summary>
    private void Bump()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _debounce ??= new System.Threading.Timer(OnDebounce, null, Timeout.Infinite, Timeout.Infinite);
            _debounce.Change(300, Timeout.Infinite);
        }
    }

    private void OnDebounce(object? state)
    {
        lock (_lock)
        {
            if (_disposed) return;
            try { Load(); }
            catch (Exception ex)
            {
                // OnDebounce tourne sur le thread du System.Threading.Timer : une exception qui s'en échappe
                // n'a personne pour l'attraper et tue le processus. Load() se protège déjà elle-même, mais ce
                // filet reste pour tout ce qui pourrait s'ajouter ici plus tard.
                Log.Error("config", $"rechargement (minuterie) : {ex.Message}");
            }
        }
    }

    /// <summary>Modifie une pilule dans la surcharge locale (créée au besoin) puis recharge.</summary>
    public bool SetPillLocal(string pillId, Action<JsonObject> mutate)
    {
        lock (_lock)
        {
            JsonObject root;
            try { root = File.Exists(LocalPath) ? CellsJson.Parse(File.ReadAllText(LocalPath)) : new JsonObject(); }
            catch (ConfigException) { root = new JsonObject(); }
            if (root["pills"] is not JsonArray pills) root["pills"] = pills = new JsonArray();
            var pill = pills.OfType<JsonObject>().FirstOrDefault(p => p["id"]?.GetValue<string>() == pillId);
            if (pill is null) pills.Add(pill = new JsonObject { ["id"] = pillId });
            mutate(pill);
            var ok = Json.WriteAtomic(LocalPath, Json.Format(root), "config");
            Load();
            return ok;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _disposed = true;
            foreach (var w in _watchers) w.Dispose();
            _watchers.Clear();
            _debounce?.Dispose();
            _debounce = null;
        }
    }
}

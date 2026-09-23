using System.Globalization;
using System.Text.Json.Nodes;
using CustomNotch.Core.Sources;

namespace CustomNotch.Core.Config;

/// <summary>La seule porte d'écriture de la fenêtre de réglages. Chaque opération relit le document concerné (pas de copie
/// en mémoire à désynchroniser), le modifie, l'écrit atomiquement, puis recharge le ConfigStore ; si la validation refuse,
/// le document précédent est réécrit et l'erreur remonte : jamais appliqué à moitié. Le fichier partagé est réécrit sans
/// ses commentaires manuels (System.Text.Json ne les conserve pas) ; un en-tête fixe le dit.</summary>
public sealed class ConfigEditor
{
    private const string Header =
        "// customNotch — cells.json édité par la fenêtre Réglages (les commentaires manuels ne sont pas conservés).\n" +
        "// Placeholders : ${env:NAME}, ${secret:name}, ${home}. Surcharge locale : cells.<machine>.json.\n";

    private readonly ConfigStore _store;
    private readonly IReadOnlyDictionary<string, SourceSchema> _schemas;

    public ConfigEditor(ConfigStore store, IReadOnlyDictionary<string, SourceSchema> schemas)
    {
        _store = store;
        _schemas = schemas;
    }

    public static string SecretName(string cellId, string field) => $"{cellId}.{field}";
    public static string SecretPlaceholder(string cellId, string field) => "${secret:" + SecretName(cellId, field) + "}";

    // ---- documents --------------------------------------------------------------------------------------------

    private JsonObject Shared()
        => File.Exists(_store.CellsPath) ? CellsJson.Parse(File.ReadAllText(_store.CellsPath)) : new JsonObject { ["version"] = 1, ["pills"] = new JsonArray() };

    private JsonObject Local()
    {
        try { return File.Exists(_store.LocalPath) ? CellsJson.Parse(File.ReadAllText(_store.LocalPath)) : new JsonObject(); }
        catch (ConfigException) { return new JsonObject(); }
    }

    private static JsonArray Pills(JsonObject root)
    {
        if (root["pills"] is not JsonArray a) root["pills"] = a = new JsonArray();
        return a;
    }

    private static JsonObject? PillNode(JsonObject root, string pillId)
        => Pills(root).OfType<JsonObject>().FirstOrDefault(p => p["id"]?.GetValue<string>() == pillId);

    private static JsonArray Cells(JsonObject pill)
    {
        if (pill["cells"] is not JsonArray a) pill["cells"] = a = new JsonArray();
        return a;
    }

    private static (JsonObject Pill, JsonObject Cell)? CellNode(JsonObject root, string cellId)
    {
        foreach (var pill in Pills(root).OfType<JsonObject>())
            foreach (var cell in Cells(pill).OfType<JsonObject>())
                if (cell["id"]?.GetValue<string>() == cellId) return (pill, cell);
        return null;
    }

    private static IEnumerable<JsonObject> AllCells(JsonObject root)
        => Pills(root).OfType<JsonObject>().SelectMany(p => Cells(p).OfType<JsonObject>());

    private static string Unique(string basis, IEnumerable<string> taken)
    {
        var set = taken.ToHashSet();
        if (!set.Contains(basis)) return basis;
        for (var i = 2; ; i++)
            if (!set.Contains($"{basis}-{i}")) return $"{basis}-{i}";
    }

    /// <summary>Écrit le partagé et recharge ; si la config est refusée, remet l'ancien texte et lève.</summary>
    private void CommitShared(JsonObject root)
    {
        var before = File.Exists(_store.CellsPath) ? File.ReadAllText(_store.CellsPath) : null;
        Json.WriteAtomic(_store.CellsPath, Header + root.ToJsonString(CellsJson.Options), "config");
        if (_store.Load()) return;
        var message = string.Join(" ; ", _store.LastErrors);
        if (before is not null) Json.WriteAtomic(_store.CellsPath, before, "config");
        _store.Load();
        throw new ConfigException(message);
    }

    private void CommitLocal(JsonObject root)
    {
        Json.WriteAtomic(_store.LocalPath, Json.Format(root), "config");
        if (!_store.Load()) throw new ConfigException(string.Join(" ; ", _store.LastErrors));
    }

    // ---- pilules ----------------------------------------------------------------------------------------------

    public string AddPill(string edge)
    {
        var root = Shared();
        var id = Unique("pill", Pills(root).OfType<JsonObject>().Select(p => p["id"]?.GetValue<string>() ?? ""));
        Pills(root).Add(new JsonObject { ["id"] = id, ["edge"] = edge, ["along"] = 0.5, ["cells"] = new JsonArray() });
        CommitShared(root);
        return id;
    }

    public void RemovePill(string pillId)
    {
        var root = Shared();
        var pill = PillNode(root, pillId) ?? throw new ConfigException($"pilule « {pillId} » introuvable");
        Pills(root).Remove(pill);
        CommitShared(root);
        var local = Local();
        if (local["pills"] is JsonArray pills && pills.OfType<JsonObject>().FirstOrDefault(p => p["id"]?.GetValue<string>() == pillId) is { } entry)
        {
            pills.Remove(entry);
            CommitLocal(local);
        }
    }

    public void SetPillShared(string pillId, Action<JsonObject> mutate)
    {
        var root = Shared();
        mutate(PillNode(root, pillId) ?? throw new ConfigException($"pilule « {pillId} » introuvable"));
        CommitShared(root);
    }

    public void SetPillLocal(string pillId, Action<JsonObject> mutate)
    {
        if (!_store.SetPillLocal(pillId, mutate)) throw new ConfigException(string.Join(" ; ", _store.LastErrors));
    }

    public void MovePill(string pillId, int delta)
    {
        var root = Shared();
        var pills = Pills(root);
        var pill = PillNode(root, pillId) ?? throw new ConfigException($"pilule « {pillId} » introuvable");
        var from = pills.IndexOf(pill);
        var to = Math.Clamp(from + delta, 0, pills.Count - 1);
        if (to == from) return;
        pills.RemoveAt(from);
        pills.Insert(to, pill);
        CommitShared(root);
    }

    // ---- cellules ---------------------------------------------------------------------------------------------

    /// <summary>Une cellule neuve : id dérivé du type (« system.disk » → « disk », suffixé si pris), libellé et glyph du
    /// schéma, params remplis avec les défauts déclarés.</summary>
    public string AddCell(string pillId, string sourceType)
    {
        var schema = _schemas.GetValueOrDefault(sourceType) ?? throw new ConfigException($"source « {sourceType} » inconnue");
        var root = Shared();
        var pill = PillNode(root, pillId) ?? throw new ConfigException($"pilule « {pillId} » introuvable");
        var id = Unique(sourceType.Split('.')[^1], AllCells(root).Select(c => c["id"]?.GetValue<string>() ?? ""));
        var cell = new JsonObject { ["id"] = id, ["source"] = sourceType, ["label"] = schema.Title };
        if (schema.DefaultGlyph is { } glyph) cell["glyph"] = glyph;
        var parameters = new JsonObject();
        foreach (var field in schema.Fields.Where(f => f.Default is not null))
            parameters[field.Name] = field.Type switch
            {
                "number" => JsonValue.Create(double.Parse(field.Default!, CultureInfo.InvariantCulture)),
                "bool" => JsonValue.Create(bool.Parse(field.Default!)),
                _ => JsonValue.Create(field.Default!),
            };
        if (parameters.Count > 0) cell["params"] = parameters;
        Cells(pill).Add(cell);
        CommitShared(root);
        return id;
    }

    /// <summary>Retire la cellule, et son id de tous les groupes qui la listaient (children, headline).</summary>
    public void RemoveCell(string cellId)
    {
        var root = Shared();
        var found = CellNode(root, cellId) ?? throw new ConfigException($"cellule « {cellId} » introuvable");
        Cells(found.Pill).Remove(found.Cell);
        foreach (var cell in AllCells(root))
        {
            if (cell["children"] is JsonArray children)
            {
                var gone = children.Where(c => c?.GetValue<string>() == cellId).ToList();
                foreach (var g in gone) children.Remove(g);
            }
            if (cell["headline"]?.GetValue<string>() == cellId) cell.Remove("headline");
        }
        CommitShared(root);
    }

    public void SetCell(string cellId, Action<JsonObject> mutate)
    {
        var root = Shared();
        mutate((CellNode(root, cellId) ?? throw new ConfigException($"cellule « {cellId} » introuvable")).Cell);
        CommitShared(root);
    }

    public void MoveCell(string cellId, int delta)
    {
        var root = Shared();
        var found = CellNode(root, cellId) ?? throw new ConfigException($"cellule « {cellId} » introuvable");
        var cells = Cells(found.Pill);
        var from = cells.IndexOf(found.Cell);
        var to = Math.Clamp(from + delta, 0, cells.Count - 1);
        if (to == from) return;
        cells.RemoveAt(from);
        cells.Insert(to, found.Cell);
        CommitShared(root);
    }

    public void MoveCellToPill(string cellId, string pillId)
    {
        var root = Shared();
        var found = CellNode(root, cellId) ?? throw new ConfigException($"cellule « {cellId} » introuvable");
        var target = PillNode(root, pillId) ?? throw new ConfigException($"pilule « {pillId} » introuvable");
        Cells(found.Pill).Remove(found.Cell);
        Cells(target).Add(found.Cell);
        CommitShared(root);
    }

    /// <summary>Masquer est un choix de poste, comme la position : il va dans la surcharge locale.</summary>
    public void SetCellVisible(string cellId, bool visible)
    {
        var shared = Shared();
        var found = CellNode(shared, cellId) ?? throw new ConfigException($"cellule « {cellId} » introuvable");
        var pillId = found.Pill["id"]!.GetValue<string>();
        SetPillLocal(pillId, pill =>
        {
            if (pill["cells"] is not JsonArray cells) pill["cells"] = cells = new JsonArray();
            var entry = cells.OfType<JsonObject>().FirstOrDefault(c => c["id"]?.GetValue<string>() == cellId);
            if (entry is null) cells.Add(entry = new JsonObject { ["id"] = cellId });
            entry["visible"] = visible;
        });
    }

    // ---- secrets et réglages globaux --------------------------------------------------------------------------

    public void SetSecret(string name, string value)
    {
        _store.Secrets.Set(name, value);
        _store.Load();
    }

    public void RemoveSecret(string name)
    {
        _store.Secrets.Remove(name);
        _store.Load();
    }

    public void SetSourceGlobal(string sourceType, Action<JsonObject> mutate)
    {
        var root = Shared();
        if (root["sources"] is not JsonObject sources) root["sources"] = sources = new JsonObject();
        if (sources[sourceType] is not JsonObject entry) sources[sourceType] = entry = new JsonObject();
        mutate(entry);
        CommitShared(root);
    }
}

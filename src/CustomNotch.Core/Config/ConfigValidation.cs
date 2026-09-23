using System.Globalization;
using System.Text.Json.Nodes;
using CustomNotch.Core.Sources;

namespace CustomNotch.Core.Config;

/// <summary>Une configuration fausse est refusée en bloc, avec le chemin du champ : jamais appliquée à moitié.</summary>
public static class ConfigValidation
{
    private static readonly string[] Edges = { "left", "right", "top", "bottom" };
    private static readonly string[] Kinds = { "ring", "value", "status", "sparkline", "group" };

    public static List<string> Validate(CellsFile file, IReadOnlySet<string> knownSources)
    {
        var errors = new List<string>();
        var pillIds = new HashSet<string>();
        var cellIds = new HashSet<string>();
        for (var i = 0; i < file.Pills.Count; i++)
        {
            var pill = file.Pills[i];
            var at = $"pills[{i}]";
            if (string.IsNullOrWhiteSpace(pill.Id)) errors.Add($"{at} : id manquant");
            else if (!pillIds.Add(pill.Id)) errors.Add($"{at} : id « {pill.Id} » en double");
            if (!Edges.Contains(pill.Edge)) errors.Add($"{at}.edge « {pill.Edge} » : attendu left, right, top ou bottom");
            if (pill.Along is < 0 or > 1) errors.Add($"{at}.along : attendu entre 0 et 1");
            if (pill.Scale is < 0.5 or > 3) errors.Add($"{at}.scale : attendu entre 0.5 et 3");
            for (var j = 0; j < pill.Cells.Count; j++)
            {
                var cell = pill.Cells[j];
                var cat = $"{at}.cells[{j}]";
                if (string.IsNullOrWhiteSpace(cell.Id)) errors.Add($"{cat} : id manquant");
                else if (!cellIds.Add(cell.Id)) errors.Add($"{cat} : cellule « {cell.Id} » en double");
                if (!cell.IsGroup && !knownSources.Contains(cell.Source)) errors.Add($"{cat}.source « {cell.Source} » inconnue");
                if (cell.Refresh is not null && cell.RefreshSpan() is null) errors.Add($"{cat}.refresh « {cell.Refresh} » : attendu 500ms, 2s, 5m ou 1h");
                if (cell.Kind is not null && !Kinds.Contains(cell.Kind.ToLowerInvariant())) errors.Add($"{cat}.kind « {cell.Kind} » : attendu ring, value, status, sparkline ou group");
            }
        }
        foreach (var cell in file.AllCells().Where(c => c.IsGroup))
        {
            foreach (var child in cell.Children!)
                if (!cellIds.Contains(child)) errors.Add($"cellule « {cell.Id} » : enfant « {child} » introuvable");
            if (cell.Headline is { } h && !cell.Children!.Contains(h)) errors.Add($"cellule « {cell.Id} » : headline « {h} » n'est pas un enfant");
        }
        DetectCycles(file, errors);
        return errors;
    }

    /// <summary>Avec les schémas : en plus des types de source, les params de chaque cellule sont vérifiés champ par champ
    /// (requis, nombre, booléen, URL absolue, choix). Une clé absente n'est pas une erreur de configuration : la source
    /// le dira elle-même à la lecture (cellule qui reste périmée) ; une clé présente mais vide — placeholder ou secret non
    /// résolu — est en revanche refusée ici, sur un champ requis.</summary>
    public static List<string> Validate(CellsFile file, IReadOnlyDictionary<string, SourceSchema> schemas)
    {
        var errors = Validate(file, schemas.Keys.ToHashSet());
        for (var i = 0; i < file.Pills.Count; i++)
            for (var j = 0; j < file.Pills[i].Cells.Count; j++)
            {
                var cell = file.Pills[i].Cells[j];
                if (cell.IsGroup || !schemas.TryGetValue(cell.Source, out var schema)) continue;
                ValidateParams(cell, $"pills[{i}].cells[{j}].params", schema, errors);
            }
        return errors;
    }

    private static void ValidateParams(CellConfig cell, string at, SourceSchema schema, List<string> errors)
    {
        foreach (var field in schema.Fields)
        {
            var node = cell.Params?[field.Name];
            if (node is null) continue;
            var text = node switch { JsonValue v when v.TryGetValue<string>(out var s) => s, JsonValue v => v.ToJsonString(), _ => null };
            if (text is { Length: 0 })
            {
                if (field.Required) errors.Add($"{at}.{field.Name} : requis (vide, placeholder ou secret manquant)");
                continue;
            }
            switch (field.Type)
            {
                case "number":
                    if (!(node is JsonValue nv && nv.TryGetValue<double>(out _)) && !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                        errors.Add($"{at}.{field.Name} « {text} » : nombre attendu");
                    break;
                case "bool":
                    if (!(node is JsonValue bv && bv.TryGetValue<bool>(out _))) errors.Add($"{at}.{field.Name} « {text} » : true ou false attendu");
                    break;
                case "url":
                    if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                        errors.Add($"{at}.{field.Name} « {text} » : URL http(s) absolue attendue");
                    break;
                case "choice":
                    if (field.Choices is { Count: > 0 } && !field.Choices.Contains(text!))
                        errors.Add($"{at}.{field.Name} « {text} » : attendu {string.Join(", ", field.Choices)}");
                    break;
            }
        }
    }

    /// <summary>Un groupe qui se contient (directement ou via d'autres groupes) ferait boucler tout code qui
    /// descend l'arbre des enfants (rendu de la carte, résolution des lectures) : sans cette garde, une config
    /// mal formée plante le processus entier (StackOverflow) au lieu d'être refusée proprement.</summary>
    private static void DetectCycles(CellsFile file, List<string> errors)
    {
        var groups = file.AllCells().Where(c => c.IsGroup).ToDictionary(c => c.Id);
        var visited = new HashSet<string>();
        var onStack = new HashSet<string>();
        var stack = new List<string>();
        foreach (var id in groups.Keys)
            if (!visited.Contains(id)) Walk(id, groups, visited, onStack, stack, errors);
    }

    private static void Walk(string id, Dictionary<string, CellConfig> groups, HashSet<string> visited, HashSet<string> onStack, List<string> stack, List<string> errors)
    {
        visited.Add(id);
        onStack.Add(id);
        stack.Add(id);
        if (groups.TryGetValue(id, out var cell))
        {
            foreach (var child in cell.Children!)
            {
                if (!groups.ContainsKey(child)) continue;   // seul un groupe peut refermer une boucle
                if (onStack.Contains(child))
                {
                    var from = stack.IndexOf(child);
                    var path = string.Join(" → ", stack.Skip(from).Append(child));
                    errors.Add($"cellule « {child} » : boucle dans children ({path})");
                }
                else if (!visited.Contains(child))
                {
                    Walk(child, groups, visited, onStack, stack, errors);
                }
            }
        }
        stack.RemoveAt(stack.Count - 1);
        onStack.Remove(id);
    }
}

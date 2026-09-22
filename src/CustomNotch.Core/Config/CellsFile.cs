using System.Text.Json.Nodes;
using CustomNotch.Core.Model;

namespace CustomNotch.Core.Config;

/// <summary>Une action : ouvrir (URL, chemin, app), lancer une commande, ou appeler une méthode de la source.</summary>
public sealed class ActionConfig
{
    public string? Label { get; set; }
    public string? Icon { get; set; }
    public string? Open { get; set; }
    public string? Shell { get; set; }
    public string? Source { get; set; }
}

public sealed class CellActions
{
    public ActionConfig? Click { get; set; }
    public List<ActionConfig>? Card { get; set; }
}

public sealed class CellConfig
{
    public string Id { get; set; } = "";
    public string Source { get; set; } = "launcher";
    public string? Label { get; set; }
    public string? Glyph { get; set; }
    /// <summary>ring | value | status | sparkline | group ; null = déduit de la lecture.</summary>
    public string? Kind { get; set; }
    /// <summary>« 2s », « 500ms », « 5m », « 1h » ; null = cadence par défaut de la source.</summary>
    public string? Refresh { get; set; }
    public Thresholds? Thresholds { get; set; }
    /// <summary>Les paramètres propres à la source (url, drive, command…), laissés en JSON : chaque source lit les siens.</summary>
    public JsonObject? Params { get; set; }
    public List<string>? Children { get; set; }
    public string? Headline { get; set; }
    public CellActions? Actions { get; set; }
    public bool Visible { get; set; } = true;

    public bool IsGroup => Children is { Count: > 0 };

    /// <summary>La cadence demandée, ou null. « 2s » → 2 s ; formats acceptés : ms, s, m, h.</summary>
    public TimeSpan? RefreshSpan()
    {
        if (string.IsNullOrWhiteSpace(Refresh)) return null;
        var s = Refresh.Trim().ToLowerInvariant();
        var unit = s.EndsWith("ms") ? "ms" : s[^1..];
        var number = s[..^unit.Length];
        if (!double.TryParse(number, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) || n <= 0) return null;
        return unit switch
        {
            "ms" => TimeSpan.FromMilliseconds(n),
            "s" => TimeSpan.FromSeconds(n),
            "m" => TimeSpan.FromMinutes(n),
            "h" => TimeSpan.FromHours(n),
            _ => null,
        };
    }
}

public sealed class PillConfig
{
    public string Id { get; set; } = "";
    /// <summary>left | right | top | bottom.</summary>
    public string Edge { get; set; } = "right";
    /// <summary>Position le long du bord, 0 = début (haut / gauche), 1 = fin.</summary>
    public double Along { get; set; } = 0.5;
    /// <summary>Nom d'écran (DeviceName WinForms) ; null = écran principal.</summary>
    public string? Screen { get; set; }
    public double Scale { get; set; } = 1.0;
    public bool Visible { get; set; } = true;
    public List<CellConfig> Cells { get; set; } = new();

    public bool IsVertical => Edge is "left" or "right";
}

/// <summary>Le fichier cells.json une fois fusionné avec la surcharge locale et les placeholders résolus.</summary>
public sealed class CellsFile
{
    public int Version { get; set; } = 1;
    public List<PillConfig> Pills { get; set; } = new();
    /// <summary>Réglages globaux par type de source (« claude »: { … }), laissés en JSON.</summary>
    public JsonObject? Sources { get; set; }

    public IEnumerable<CellConfig> AllCells() => Pills.SelectMany(p => p.Cells);
    public CellConfig? Cell(string id) => AllCells().FirstOrDefault(c => c.Id == id);
}

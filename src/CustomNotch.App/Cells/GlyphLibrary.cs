using System.Windows.Media;

namespace CustomNotch.App.Cells;

/// <summary>Les icônes des cellules par nom ; un tracé SVG brut (« M… ») est accepté tel quel dans cells.json. Les
/// icônes au trait sont dans une grille 16 × 16 comme Glyphs.cs ; « claude » est le logo plein (codenotch, MIT),
/// grille 24 × 24.</summary>
public static class GlyphLibrary
{
    private static readonly Dictionary<string, (string Path, bool Filled)> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["dot"] = ("M8,8 L8,8.05", false),
        ["cpu"] = ("M4.5,4.5 L11.5,4.5 L11.5,11.5 L4.5,11.5 Z M6.5,6.5 L9.5,6.5 L9.5,9.5 L6.5,9.5 Z M6,2 L6,4.5 M10,2 L10,4.5 M6,11.5 L6,14 M10,11.5 L10,14 M2,6 L4.5,6 M2,10 L4.5,10 M11.5,6 L14,6 M11.5,10 L14,10", false),
        ["memory"] = ("M2,5 L14,5 L14,11 L2,11 Z M4,11 L4,13.5 M7,11 L7,13.5 M9,11 L9,13.5 M12,11 L12,13.5 M4.5,7 L4.5,9 M7,7 L7,9 M9.5,7 L9.5,9 M12,7 L12,9", false),
        ["disk"] = ("M8,2.5 A5.5,5.5 0 1 1 7.99,2.5 Z M8,6.5 A1.5,1.5 0 1 1 7.99,6.5 Z M13,10 L10.5,8.5", false),
        ["network"] = ("M8,2.5 L8,13.5 M4.5,6 L8,2.5 L11.5,6 M4.5,10 L8,13.5 L11.5,10", false),
        ["battery"] = ("M2.5,5 L12.5,5 L12.5,11 L2.5,11 Z M12.5,7 L14,7 L14,9 L12.5,9 M4.5,7 L8,7 L8,9 L4.5,9 Z", false),
        ["link"] = ("M6.5,9.5 L9.5,6.5 M7,4.8 L8.4,3.4 A2.6,2.6 0 0 1 12.6,7.6 L11.2,9 M9,11.2 L7.6,12.6 A2.6,2.6 0 0 1 3.4,8.4 L4.8,7", false),
        ["globe"] = ("M8,2.5 A5.5,5.5 0 1 1 7.99,2.5 Z M2.5,8 L13.5,8 M8,2.5 C5.5,5 5.5,11 8,13.5 M8,2.5 C10.5,5 10.5,11 8,13.5", false),
        ["terminal"] = ("M2.5,3 L13.5,3 L13.5,13 L2.5,13 Z M5,6 L7.5,8 L5,10 M8.5,10.5 L11,10.5", false),
        ["folder"] = ("M2.5,4 L6.5,4 L8,5.5 L13.5,5.5 L13.5,12.5 L2.5,12.5 Z", false),
        ["open"] = ("M6.5,3 L3,3 L3,13 L13,13 L13,9.5 M9,3 L13,3 L13,7 M13,3 L7.5,8.5", false),
        ["play"] = ("M0,0 L10,5 L0,10 Z", true),
        ["pause"] = ("M0,0 L3,0 L3,10 L0,10 Z M6,0 L9,0 L9,10 L6,10 Z", true),
        // Précédent / suivant pleins, dans la même boîte 10×10 que play/pause : un triangle et une barre, comme les
        // touches d'un lecteur — les chevrons filaires d'avant se lisaient mal à 16 px à côté des boutons pleins.
        ["prev"] = ("M0,0 L2,0 L2,10 L0,10 Z M10,0 L3,5 L10,10 Z", true),
        ["next"] = ("M8,0 L10,0 L10,10 L8,10 Z M0,0 L7,5 L0,10 Z", true),
        ["refresh"] = ("M13,8 A5,5 0 1 1 11.6,4.4 M11.8,2.2 L11.8,4.8 L9.2,4.8", false),
        ["gear"] = ("M8,5.6 A2.4,2.4 0 1 1 7.99,5.6 Z M8,1.5 L8,3.4 M8,12.6 L8,14.5 M1.5,8 L3.4,8 M12.6,8 L14.5,8 M3.4,3.4 L4.75,4.75 M11.25,11.25 L12.6,12.6 M3.4,12.6 L4.75,11.25 M11.25,4.75 L12.6,3.4", false),
        ["bell"] = ("M4,11.5 L12,11.5 L11,10 L11,7 A3,3 0 0 0 5,7 L5,10 Z M6.8,13.5 L9.2,13.5", false),
        ["clock"] = ("M8,2.5 A5.5,5.5 0 1 1 7.99,2.5 Z M8,4.8 L8,8.2 L10.6,9.6", false),
        ["chart"] = ("M3,13.5 L13,13.5 M5,13.5 L5,8.5 M8,13.5 L8,4.5 M11,13.5 L11,10", false),
        ["music"] = ("M6,12 A2,2 0 1 1 5.99,12 Z M12,10.5 A2,2 0 1 1 11.99,10.5 Z M8,12 L8,4 L14,2.5 L14,10.5", false),
        ["check"] = ("M2.5,8.5 L6.5,12.5 L13.5,4", false),
        ["claude"] = ("M4.709 15.955l4.72-2.647.08-.23-.08-.128H9.2l-.79-.048-2.698-.073-2.339-.097-2.266-.122-.571-.121L0 11.784l.055-.352.48-.321.686.06 1.52.103 2.278.158 1.652.097 2.449.255h.389l.055-.157-.134-.098-.103-.097-2.358-1.596-2.552-1.688-1.336-.972-.724-.491-.364-.462-.158-1.008.656-.722.881.06.225.061.893.686 1.908 1.476 2.491 1.833.365.304.145-.103.019-.073-.164-.274-1.355-2.446-1.446-2.49-.644-1.032-.17-.619a2.97 2.97 0 01-.104-.729L6.283.134 6.696 0l.996.134.42.364.62 1.414 1.002 2.229 1.555 3.03.456.898.243.832.091.255h.158V9.01l.128-1.706.237-2.095.23-2.695.08-.76.376-.91.747-.492.584.28.48.685-.067.444-.286 1.851-.559 2.903-.364 1.942h.212l.243-.242.985-1.306 1.652-2.064.73-.82.85-.904.547-.431h1.033l.76 1.129-.34 1.166-1.064 1.347-.881 1.142-1.264 1.7-.79 1.36.073.11.188-.02 2.856-.606 1.543-.28 1.841-.315.833.388.091.395-.328.807-1.969.486-2.309.462-3.439.813-.042.03.049.061 1.549.146.662.036h1.622l3.02.225.79.522.474.638-.079.485-1.215.62-1.64-.389-3.829-.91-1.312-.329h-.182v.11l1.093 1.068 2.006 1.81 2.509 2.33.127.578-.322.455-.34-.049-2.205-1.657-.851-.747-1.926-1.62h-.128v.17l.444.649 2.345 3.521.122 1.08-.17.353-.608.213-.668-.122-1.374-1.925-1.415-2.167-1.143-1.943-.14.08-.674 7.254-.316.37-.729.28-.607-.461-.322-.747.322-1.476.389-1.924.315-1.53.286-1.9.17-.632-.012-.042-.14.018-1.434 1.967-2.18 2.945-1.726 1.845-.414.164-.717-.37.067-.662.401-.589 2.388-3.036 1.44-1.882.93-1.086-.006-.158h-.055L4.132 18.56l-1.13.146-.487-.456.061-.746.231-.243 1.908-1.312-.006.006z", true),
    };

    private static readonly Dictionary<string, (Geometry, bool)> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Les noms de glyphes utilisables dans la galerie, hors « dot » (le point par défaut, pas un choix).</summary>
    public static IEnumerable<string> Names => Named.Keys.Where(k => k != "dot").OrderBy(k => k);

    public static (Geometry Data, bool Filled) Get(string? name)
    {
        var key = string.IsNullOrWhiteSpace(name) ? "dot" : name.Trim();
        lock (Cache)
        {
            if (Cache.TryGetValue(key, out var hit)) return hit;
            (Geometry, bool) result;
            if (Named.TryGetValue(key, out var named)) result = (Parse(named.Path), named.Filled);
            else if (key.StartsWith('M') || key.StartsWith("path:", StringComparison.OrdinalIgnoreCase))
            {
                try { result = (Parse(key.StartsWith("path:", StringComparison.OrdinalIgnoreCase) ? key[5..] : key), true); }
                catch (FormatException) { result = (Parse(Named["dot"].Path), false); }
            }
            else result = (Parse(Named["dot"].Path), false);
            Cache[key] = result;
            return result;
        }
    }

    private static Geometry Parse(string path)
    {
        var g = Geometry.Parse(path);
        g.Freeze();
        return g;
    }
}

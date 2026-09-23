using System.Globalization;
using CustomNotch.Core.Config;
using CustomNotch.Core.Sources;

namespace CustomNotch.Core.Model;

/// <summary>Ce que l'interface dessine pour une cellule : calculé d'une config et d'une lecture, sans WPF,
/// pour être testé sans écran.</summary>
public sealed record CellView(
    string Id,
    CellKind Kind,
    Status Status,
    string Label,
    string? Glyph,
    string? Caption,
    double? Fraction,
    bool Stale,
    string? StaleAge,
    Reading Reading,
    byte[]? Image,
    string Shape,
    string Activity,
    bool ShowCaption);

public static class CellViews
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    public static CellView From(CellConfig cell, Reading r, long nowMs, PillConfig? pill = null, AppearanceConfig? appearance = null, SourceSchema? schema = null)
    {
        var kind = DeriveKind(cell, r);
        var status = DeriveStatus(cell, r);
        var stale = r.StaleSinceMs is not null;
        var (shape, activity, showCaption) = Resolve(cell, pill, appearance, schema);
        return new CellView(cell.Id, kind, status, cell.Label ?? cell.Id, cell.Glyph, Caption(kind, r), Fraction(r), stale,
            stale ? Age(nowMs - r.StaleSinceMs!.Value) : null, r, r.Image, shape, activity, showCaption);
    }

    /// <summary>Ce que la cellule montre au-delà de sa lecture : la forme vient de la pilule, l'activité de la cellule
    /// sinon de l'apparence globale, la légende de la cellule sinon du schéma de sa source. Pur, pour être testé.</summary>
    public static (string Shape, string Activity, bool ShowCaption) Resolve(CellConfig cell, PillConfig? pill, AppearanceConfig? appearance, SourceSchema? schema)
        => (pill?.CellsShape ?? "round", cell.Activity ?? appearance?.Activity ?? "dot", cell.Caption ?? schema?.DefaultCaption ?? true);

    /// <summary>L'ordre de décision : les enfants font toujours un groupe (la carte hover liste ses enfants, même
    /// si un kind a été forcé par erreur), sinon le kind explicite de la config, sinon la déduction depuis la
    /// lecture. Un maximum fait un anneau même avec un historique (l'anneau l'emporte) ; la sparkline est pour ce
    /// qui n'a pas de maximum mais garde un historique (réseau, par exemple).</summary>
    public static CellKind DeriveKind(CellConfig cell, Reading r)
    {
        if (cell.IsGroup) return CellKind.Group;
        switch (cell.Kind?.Trim().ToLowerInvariant())
        {
            case "ring": return CellKind.Ring;
            case "value": return CellKind.Value;
            case "status": return CellKind.Status;
            case "sparkline": return CellKind.Sparkline;
            case "group": return CellKind.Group;
        }
        if (r.Max is not null && r.Value is not null) return CellKind.Ring;
        if (r.History is { Count: > 1 }) return CellKind.Sparkline;
        if (r.Value is not null) return CellKind.Value;
        return CellKind.Status;
    }

    public static Status DeriveStatus(CellConfig cell, Reading r)
    {
        if (r.Status is { } s) return s;
        if (r.Value is null) return Status.Off;
        var value = r.Max is { } max and > 0 ? r.Value.Value / max * 100 : r.Value.Value;
        var thresholds = cell.Thresholds ?? (r.Max is not null ? Thresholds.RingDefault : new Thresholds());
        return thresholds.Judge(value);
    }

    public static double? Fraction(Reading r)
        => r.Max is { } max and > 0 && r.Value is { } v ? Math.Clamp(v / max, 0, 1) : null;

    /// <summary>Le texte sous la cellule : « 73% » pour un anneau, « 42 °C » pour une valeur, rien pour un statut.</summary>
    public static string? Caption(CellKind kind, Reading r)
    {
        switch (kind)
        {
            case CellKind.Ring:
                return Fraction(r) is { } f ? $"{Math.Round(f * 100)}%" : null;
            case CellKind.Value:
            case CellKind.Sparkline:
                if (r.Value is not { } v) return r.Text;
                var number = Math.Abs(v) >= 100 || v == Math.Round(v) ? Math.Round(v).ToString("N0", Fr) : v.ToString("0.#", Fr);
                // "N0" en fr-FR insère une espace insécable fine (U+202F) ; on la normalise en espace simple pour
                // que « 1 240 € » corresponde à ce qu'on affiche et à ce que les tests attendent.
                number = number.Replace(' ', ' ').Replace(' ', ' ');
                return string.IsNullOrEmpty(r.Unit) ? number : $"{number} {r.Unit}";
            default:
                // Une lecture sans valeur mais avec un texte (média : le titre) : dix caractères sous la cellule.
                return r.Value is null && r.Text is { Length: > 0 } t ? (t.Length <= 10 ? t : t[..10].TrimEnd() + "…") : null;
        }
    }

    /// <summary>L'ordre d'alarme : ce qui réclame l'utilisateur d'abord, puis ce qui est critique, puis le travail en cours.</summary>
    private static int Rank(Status s) => s switch
    {
        Status.Attention => 0, Status.Crit => 1, Status.Busy => 2, Status.Warn => 3, Status.Ok => 4, _ => 5,
    };

    /// <summary>La vue d'un groupe : l'anneau vient de l'enfant `headline` (ou, à défaut, du pire enfant s'il a
    /// une fraction, sinon du premier enfant qui en a une, sinon du pire) ; le statut est toujours le pire des
    /// enfants ; le groupe n'est périmé que si tous ses enfants le sont.</summary>
    public static CellView FromGroup(CellConfig group, IReadOnlyList<CellView> children, long nowMs, PillConfig? pill = null, AppearanceConfig? appearance = null)
    {
        var (shape, activity, showCaption) = Resolve(group, pill, appearance, null);
        if (children.Count == 0)
            return new CellView(group.Id, CellKind.Group, Status.Off, group.Label ?? group.Id, group.Glyph, null, null, false, null, Reading.Empty, null, shape, activity, showCaption);
        var worst = children.MinBy(c => Rank(c.Status))!;
        var headline = (group.Headline is { } h ? children.FirstOrDefault(c => c.Id == h) : null)
            ?? (worst.Fraction is not null ? worst : children.FirstOrDefault(c => c.Fraction is not null) ?? worst);
        var stale = children.All(c => c.Stale);
        var oldest = stale ? children.Min(c => c.Reading.StaleSinceMs ?? nowMs) : (long?)null;
        return new CellView(group.Id, CellKind.Group, worst.Status, group.Label ?? group.Id, group.Glyph, headline.Caption, headline.Fraction, stale,
            stale ? Age(nowMs - oldest!.Value) : null, headline.Reading, headline.Image, shape, activity, showCaption);
    }

    public static string Age(long ms)
    {
        var seconds = Math.Max(0, ms / 1000);
        if (seconds < 60) return "à l'instant";
        if (seconds < 3600) return $"il y a {seconds / 60} min";
        if (seconds < 86400) return $"il y a {seconds / 3600} h";
        return $"il y a {seconds / 86400} j";
    }
}

using CustomNotch.Core.Config;
using CustomNotch.Core.Model;

namespace CustomNotch.App.Notch;

public sealed record CardRow(string Label, string? Text, double? Fraction, string? Hint, Status Tone);
/// <summary>TargetCellId n'est renseigné que pour une action d'un enfant de groupe (la cellule à invoquer n'est
/// pas celle qui a ouvert la carte) ; sinon null, et l'appelant vise la cellule de la carte elle-même. Fini le
/// codage « child:action » dans SourceAction, qui appliquait le découpage même quand « action » lui-même
/// contenait un « : » (ex. une action nommée par une source).</summary>
public sealed record CardAction(string Label, string? Icon, ActionConfig? Config, string? SourceAction, string? TargetCellId = null);
public sealed record CardModel(string Title, string? Glyph, string? Subtitle, IReadOnlyList<CardRow> Rows, IReadOnlyList<CardAction> Actions, string? Note, byte[]? Image);

/// <summary>Ce que la carte affiche, calculé sans WPF : les lignes de détail de la source (ou la valeur seule), les
/// enfants pour un groupe, les boutons déclarés dans la config puis ceux de la source, et la note de péremption.</summary>
public static class CardContent
{
    public static CardModel Build(CellView view, CellConfig cell, IReadOnlyList<CellView> children)
    {
        var rows = new List<CardRow>();
        var actions = new List<CardAction>();
        string title = view.Label;
        string? subtitle;
        byte[]? image = view.Image;
        if (view.Kind == CellKind.Group)
        {
            subtitle = GroupSubtitle(children);
            foreach (var child in children)
            {
                var childDetail = child.Reading.Detail is { Count: > 0 } d ? d[0] : null;
                rows.Add(new CardRow(child.Label, ChildText(child, childDetail), child.Fraction, childDetail?.Hint, child.Status));
                foreach (var a in child.Reading.Actions ?? Array.Empty<ActionSpec>())
                    actions.Add(new CardAction($"{child.Label} · {a.Label}", a.Icon, null, a.Id, child.Id));
            }
        }
        else if (cell.Source == "media")
        {
            (title, subtitle) = BuildMedia(view, rows);
        }
        else
        {
            subtitle = view.Reading.Text is { } t && view.Kind != CellKind.Value ? t : null;
            if (view.Reading.Detail is { Count: > 0 } detail)
            {
                foreach (var d in detail)
                    rows.Add(new CardRow(d.Label, d.Text, d.Fraction, d.Hint, d.Tone ?? (d.Fraction is { } f ? (cell.Thresholds ?? Thresholds.RingDefault).Judge(f * 100) : view.Status)));
            }
            else if (view.Caption is not null || view.Reading.Text is not null)
            {
                rows.Add(new CardRow(view.Label, view.Caption ?? view.Reading.Text, view.Fraction, null, view.Status));
            }
        }
        foreach (var a in cell.Actions?.Card ?? new List<ActionConfig>())
            actions.Add(new CardAction(a.Label ?? a.Open ?? a.Shell ?? a.Source ?? "Action", a.Icon, a, null));
        if (view.Kind != CellKind.Group)
            foreach (var a in view.Reading.Actions ?? Array.Empty<ActionSpec>())
                actions.Add(new CardAction(a.Label, a.Icon, null, a.Id));
        var note = view.Stale ? $"Lecture {view.StaleAge} · {view.Reading.Error ?? "source injoignable"}" : null;
        return new CardModel(title, view.Glyph, subtitle, rows, actions, note, image);
    }

    /// <summary>Le texte d'une ligne d'enfant de groupe : le pourcentage (Caption) seul quand l'enfant n'a pas de
    /// détail, sinon le pourcentage suivi de la valeur absolue quand elle diffère (« 61% · 9,8 Go ») — le lecteur
    /// veut le relatif ET l'absolu, pas l'un ou l'autre.</summary>
    private static string? ChildText(CellView child, DetailRow? detail)
    {
        var caption = child.Caption ?? child.Reading.Text;
        if (detail?.Text is not { } absolute || absolute == caption) return caption;
        return caption is null ? absolute : $"{caption} · {absolute}";
    }

    /// <summary>Le sous-titre de synthèse d'un groupe (carte Système) : le pire enfant décide — critique d'abord,
    /// à surveiller ensuite, sinon tout va bien.</summary>
    private static string GroupSubtitle(IReadOnlyList<CellView> children)
    {
        var crit = children.FirstOrDefault(c => c.Status == Status.Crit);
        if (crit is not null) return $"{crit.Label} critique";
        var warn = children.FirstOrDefault(c => c.Status == Status.Warn);
        return warn is not null ? $"{warn.Label} à surveiller" : "tout va bien";
    }

    /// <summary>L'en-tête et le corps d'une carte média : titre = la piste, sous-titre = l'artiste (coupés sur le
    /// premier « — » de Reading.Text) et une seule ligne (la position) quand une lecture est en cours ; sans
    /// lecture, le titre reste le libellé de la cellule et la ligne dit pourquoi cliquer.</summary>
    private static (string Title, string? Subtitle) BuildMedia(CellView view, List<CardRow> rows)
    {
        if (view.Status == Status.Off)
        {
            foreach (var d in view.Reading.Detail ?? Array.Empty<DetailRow>())
                rows.Add(new CardRow(d.Label, d.Text, d.Fraction, d.Hint, view.Status));
            return (view.Label, null);
        }
        var text = view.Reading.Text ?? "";
        var sep = text.IndexOf(" — ", StringComparison.Ordinal);
        var title = sep >= 0 ? text[..sep] : text;
        var subtitle = sep >= 0 ? text[(sep + 3)..] : null;
        if (view.Reading.Detail is { Count: > 0 } d0 && d0[0].Label == "position")
            rows.Add(new CardRow(d0[0].Label, d0[0].Text, d0[0].Fraction, d0[0].Hint, view.Status));
        return (title, subtitle);
    }
}

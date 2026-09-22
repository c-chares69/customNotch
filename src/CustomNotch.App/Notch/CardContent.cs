using CustomNotch.Core.Config;
using CustomNotch.Core.Model;

namespace CustomNotch.App.Notch;

public sealed record CardRow(string Label, string? Text, double? Fraction, string? Hint, Status Tone);
public sealed record CardAction(string Label, string? Icon, ActionConfig? Config, string? SourceAction);
public sealed record CardModel(string Title, string? Glyph, string? Subtitle, IReadOnlyList<CardRow> Rows, IReadOnlyList<CardAction> Actions, string? Note);

/// <summary>Ce que la carte affiche, calculé sans WPF : les lignes de détail de la source (ou la valeur seule), les
/// enfants pour un groupe, les boutons déclarés dans la config puis ceux de la source, et la note de péremption.</summary>
public static class CardContent
{
    public static CardModel Build(CellView view, CellConfig cell, IReadOnlyList<CellView> children)
    {
        var rows = new List<CardRow>();
        var actions = new List<CardAction>();
        if (view.Kind == CellKind.Group)
        {
            foreach (var child in children)
            {
                var hint = child.Reading.Detail is { Count: > 0 } d ? d[0].Hint : null;
                rows.Add(new CardRow(child.Label, child.Caption ?? child.Reading.Text, child.Fraction, hint, child.Status));
                foreach (var a in child.Reading.Actions ?? Array.Empty<ActionSpec>())
                    actions.Add(new CardAction($"{child.Label} · {a.Label}", a.Icon, null, $"{child.Id}:{a.Id}"));
            }
        }
        else if (view.Reading.Detail is { Count: > 0 } detail)
        {
            foreach (var d in detail)
                rows.Add(new CardRow(d.Label, d.Text, d.Fraction, d.Hint, d.Tone ?? (d.Fraction is { } f ? (cell.Thresholds ?? Thresholds.RingDefault).Judge(f * 100) : view.Status)));
        }
        else if (view.Caption is not null || view.Reading.Text is not null)
        {
            rows.Add(new CardRow(view.Label, view.Caption ?? view.Reading.Text, view.Fraction, null, view.Status));
        }
        foreach (var a in cell.Actions?.Card ?? new List<ActionConfig>())
            actions.Add(new CardAction(a.Label ?? a.Open ?? a.Shell ?? a.Source ?? "Action", a.Icon, a, null));
        if (view.Kind != CellKind.Group)
            foreach (var a in view.Reading.Actions ?? Array.Empty<ActionSpec>())
                actions.Add(new CardAction(a.Label, a.Icon, null, a.Id));
        var note = view.Stale ? $"Lecture {view.StaleAge} · {view.Reading.Error ?? "source injoignable"}" : null;
        return new CardModel(view.Label, view.Glyph, view.Reading.Text is { } t && view.Kind != CellKind.Value ? t : null, rows, actions, note);
    }
}

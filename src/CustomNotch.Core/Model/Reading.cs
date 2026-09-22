namespace CustomNotch.Core.Model;

/// <summary>Une ligne de la carte hover : libellé, texte à droite, barre 0..1 facultative, indication (« reset dans 51 min »).</summary>
public sealed record DetailRow(string Label, string? Text = null, double? Fraction = null, string? Hint = null, Status? Tone = null);

/// <summary>Un bouton de la carte : l'identifiant est ce que la source reçoit dans InvokeAsync.</summary>
public sealed record ActionSpec(string Id, string Label, string? Icon = null);

/// <summary>Ce qu'une source produit, et la seule chose que l'interface connaît. Tout est facultatif : une source
/// dit ce qu'elle sait, et CellViews en déduit le rendu.</summary>
public sealed record Reading(
    double? Value = null,
    double? Max = null,
    string? Unit = null,
    string? Text = null,
    Status? Status = null,
    IReadOnlyList<DetailRow>? Detail = null,
    IReadOnlyList<ActionSpec>? Actions = null,
    IReadOnlyList<(long Ms, double V)>? History = null,
    long? StaleSinceMs = null,
    string? Error = null)
{
    public static readonly Reading Empty = new();

    /// <summary>La même lecture, marquée périmée depuis maintenant (ou depuis la première panne) avec la raison.</summary>
    public Reading AsStale(long nowMs, string error) => this with { StaleSinceMs = StaleSinceMs ?? nowMs, Error = error };

    /// <summary>La lecture rafraîchie : plus périmée, plus d'erreur.</summary>
    public Reading Fresh() => this with { StaleSinceMs = null, Error = null };
}

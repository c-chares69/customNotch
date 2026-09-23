namespace CustomNotch.Core.Sources.Claude;

/// <summary>Une fenêtre de limite (session 5 h, semaine tous modèles, semaine par modèle…) telle que dédoublonnée
/// par <see cref="UsageParser"/> - un id stable, un libellé français, le pourcentage consommé, et l'heure de
/// réinitialisation si l'API la donne.</summary>
public sealed record LimitWindow(string Id, string Label, double Percent, long? ResetsAtMs);

/// <summary>Une ligne de la répartition hebdomadaire par surface (Claude Code, Chats, Cowork…).</summary>
public sealed record UsageBreakdown(string Label, double Percent);

/// <summary>L'usage tel que lu à un instant donné : les fenêtres, la répartition, et quand c'était - pour que
/// l'appelant sache si une lecture périmée doit être montrée plutôt qu'inventer un chiffre.</summary>
public sealed record UsageSnapshot(IReadOnlyList<LimitWindow> Windows, IReadOnlyList<UsageBreakdown> Breakdown, long ReadAtMs, string? Plan);

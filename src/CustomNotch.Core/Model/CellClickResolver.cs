using CustomNotch.Core.Config;
using CustomNotch.Core.Sources;

namespace CustomNotch.Core.Model;

/// <summary>Ce que fait un clic : l'action de la config, sinon celle que la source déclare par défaut, sinon la carte.</summary>
public sealed record ClickPlan(ActionConfig? Config, string? SourceAction, bool OpenCard);

public static class CellClickResolver
{
    public static ClickPlan Resolve(CellConfig cell, SourceSchema? schema)
    {
        if (cell.Actions?.Click is { } click && (click.Open ?? click.Shell ?? click.Source) is { Length: > 0 })
            return new ClickPlan(click, null, false);
        if (!cell.IsGroup && schema?.DefaultAction is { Length: > 0 } action)
            return new ClickPlan(null, action, false);
        return new ClickPlan(null, null, true);
    }
}

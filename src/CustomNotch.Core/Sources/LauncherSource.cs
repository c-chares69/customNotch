using CustomNotch.Core.Model;

namespace CustomNotch.Core.Sources;

/// <summary>Pas de donnée : un glyph et une action. C'est ce qui remplace un raccourci de barre des tâches.</summary>
public sealed class LauncherSource : SourceBase
{
    public override string Type => "launcher";
    public override SourceSchema Schema => new(Type, "Lanceur", new[] { new SchemaField("open", "string", "Ouvrir", Required: true, Help: "URL, chemin, ou application") }, "link");
    public override TimeSpan DefaultRefresh => TimeSpan.FromHours(24);

    public override Task<Reading> ReadAsync(CellContext ctx, CancellationToken ct)
        => Task.FromResult(new Reading(Status: Status.Off, Text: ctx.Str("open"), Actions: new[] { new ActionSpec("open", "Ouvrir", "open") }));

    public override Task InvokeAsync(string action, CellContext ctx, CancellationToken ct)
    {
        if (action == "open" && ctx.Str("open") is { } target) Actions.ActionRunner.Open(target);
        return Task.CompletedTask;
    }
}

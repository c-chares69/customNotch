using System.Text.Json.Nodes;
using CustomNotch.Core.Model;

namespace CustomNotch.Core.Sources;

/// <summary>Ce qu'une source reçoit : l'id de la cellule, ses params, et les réglages globaux de son type
/// (cells.json → "sources": { "clickup": { … } }).</summary>
public sealed record CellContext(string CellId, JsonObject Params, JsonObject? Globals)
{
    public string? Str(string key) => Params[key] is JsonValue v && v.TryGetValue<string>(out var s) && s.Length > 0 ? s
        : Globals?[key] is JsonValue g && g.TryGetValue<string>(out var gs) && gs.Length > 0 ? gs : null;
    public double? Num(string key) => Params[key] is JsonValue v && v.TryGetValue<double>(out var d) ? d : null;
    public bool Flag(string key, bool fallback = false) => Params[key] is JsonValue v && v.TryGetValue<bool>(out var b) ? b : fallback;
}

/// <summary>Un champ de paramètre : la fenêtre de réglages en fait un formulaire, la validation le vérifie.
/// Type : string | number | bool | secret | path | url | choice.</summary>
public sealed record SchemaField(string Name, string Type, string Label, bool Required = false, string? Help = null, string? Default = null, IReadOnlyList<string>? Choices = null);

public sealed record SourceSchema(string Type, string Title, IReadOnlyList<SchemaField> Fields, string? DefaultGlyph = null);

/// <summary>Le contrat de toute source : lire, agir, et pousser une mise à jour hors cadence.</summary>
public interface ISource
{
    string Type { get; }
    SourceSchema Schema { get; }
    TimeSpan DefaultRefresh { get; }
    Task<Reading> ReadAsync(CellContext ctx, CancellationToken ct);
    Task InvokeAsync(string action, CellContext ctx, CancellationToken ct);
    /// <summary>« Relis cette cellule maintenant » (hook Claude, changement de piste média…).</summary>
    event Action<string>? Pushed;
}

public abstract class SourceBase : ISource
{
    public abstract string Type { get; }
    public abstract SourceSchema Schema { get; }
    public virtual TimeSpan DefaultRefresh => TimeSpan.FromSeconds(30);
    public abstract Task<Reading> ReadAsync(CellContext ctx, CancellationToken ct);
    public virtual Task InvokeAsync(string action, CellContext ctx, CancellationToken ct) => Task.CompletedTask;
    public event Action<string>? Pushed;
    protected void Push(string cellId) => Pushed?.Invoke(cellId);
}

using CustomNotch.Core.Model;
using CustomNotch.Core.Platform;

namespace CustomNotch.Core.Sources.System;

public sealed class CpuSource : SourceBase
{
    private sealed class State
    {
        public (ulong Idle, ulong Kernel, ulong User)? Previous;
        public readonly Queue<(long, double)> History = new();
    }

    // Une instance de la source sert toutes les cellules de ce type : l'état (delta CPU, historique)
    // doit donc être gardé par cellule, pas dans des champs d'instance partagés entre elles.
    private readonly Dictionary<string, State> _cells = new();
    private readonly object _lock = new();

    public override string Type => "system.cpu";
    public override SourceSchema Schema => new(Type, "Processeur", Array.Empty<SchemaField>(), "cpu", "Occupation du processeur");
    public override TimeSpan DefaultRefresh => TimeSpan.FromSeconds(2);

    public override Task<Reading> ReadAsync(CellContext ctx, CancellationToken ct)
    {
        var now = SystemInfo.CpuTimes() ?? throw new InvalidOperationException("Temps CPU indisponibles sur ce système.");
        List<(long, double)> history;
        double percent;
        lock (_lock)
        {
            if (!_cells.TryGetValue(ctx.CellId, out var state)) _cells[ctx.CellId] = state = new State();
            percent = state.Previous is { } prev ? Units.CpuPercent(prev, now) : 0;
            state.Previous = now;
            History.Keep(state.History, percent, 60);
            history = state.History.ToList();
        }
        return Task.FromResult(new Reading(Value: Math.Round(percent), Max: 100, Unit: "%",
            Detail: new[] { new DetailRow("Occupation", $"{Math.Round(percent)} %", percent / 100) },
            History: history));
    }
}

/// <summary>Un historique borné pour les sparklines : N derniers points, horodatés.</summary>
internal static class History
{
    public static void Keep(Queue<(long Ms, double V)> queue, double value, int max)
    {
        queue.Enqueue((DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), value));
        while (queue.Count > max) queue.Dequeue();
    }
}

using CustomNotch.Core.Model;
using CustomNotch.Core.Platform;

namespace CustomNotch.Core.Sources.System;

public sealed class CpuSource : SourceBase
{
    private (ulong Idle, ulong Kernel, ulong User)? _previous;
    private readonly Queue<(long, double)> _history = new();

    public override string Type => "system.cpu";
    public override SourceSchema Schema => new(Type, "Processeur", Array.Empty<SchemaField>(), "cpu");
    public override TimeSpan DefaultRefresh => TimeSpan.FromSeconds(2);

    public override Task<Reading> ReadAsync(CellContext ctx, CancellationToken ct)
    {
        var now = SystemInfo.CpuTimes() ?? throw new InvalidOperationException("Temps CPU indisponibles sur ce système.");
        var percent = _previous is { } prev ? Units.CpuPercent(prev, now) : 0;
        _previous = now;
        History.Keep(_history, percent, 60);
        return Task.FromResult(new Reading(Value: Math.Round(percent), Max: 100, Unit: "%",
            Detail: new[] { new DetailRow("Occupation", $"{Math.Round(percent)} %", percent / 100) },
            History: _history.ToList()));
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

using CustomNotch.Core.Model;
using CustomNotch.Core.Platform;

namespace CustomNotch.Core.Sources.System;

public sealed class MemorySource : SourceBase
{
    public override string Type => "system.memory";
    public override SourceSchema Schema => new(Type, "Mémoire", Array.Empty<SchemaField>(), "memory");
    public override TimeSpan DefaultRefresh => TimeSpan.FromSeconds(5);

    public override Task<Reading> ReadAsync(CellContext ctx, CancellationToken ct)
    {
        var (total, available) = SystemInfo.Memory() ?? throw new InvalidOperationException("Mémoire indisponible sur ce système.");
        var used = total - available;
        return Task.FromResult(new Reading(Value: Units.Gb(used), Max: Units.Gb(total), Unit: "Go",
            Detail: new[]
            {
                new DetailRow("Utilisée", $"{Units.Gb(used)} Go", (double)used / total),
                new DetailRow("Libre", $"{Units.Gb(available)} Go"),
            }));
    }
}

using CustomNotch.Core.Model;

namespace CustomNotch.Core.Sources.System;

public sealed class DiskSource : SourceBase
{
    public override string Type => "system.disk";
    public override SourceSchema Schema => new(Type, "Disque", new[] { new SchemaField("drive", "string", "Lecteur", Required: true, Help: "C:", Default: "C:") }, "disk", "Espace utilisé d'un lecteur");
    public override TimeSpan DefaultRefresh => TimeSpan.FromMinutes(1);

    public override Task<Reading> ReadAsync(CellContext ctx, CancellationToken ct)
    {
        var letter = (ctx.Str("drive") ?? "C:").TrimEnd('\\', '/');
        var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.Name.TrimEnd('\\').Equals(letter, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Lecteur {letter} introuvable.");
        if (!drive.IsReady) throw new InvalidOperationException($"Lecteur {letter} non prêt.");
        var total = (ulong)drive.TotalSize;
        var free = (ulong)drive.AvailableFreeSpace;
        var used = total - free;
        return Task.FromResult(new Reading(Value: Units.Gb(used), Max: Units.Gb(total), Unit: "Go",
            Detail: new[]
            {
                new DetailRow($"{letter} {drive.VolumeLabel}".Trim(), $"{Units.Gb(used)} / {Units.Gb(total)} Go", (double)used / total),
                new DetailRow("Libre", $"{Units.Gb(free)} Go"),
            },
            Actions: new[] { new ActionSpec("open", "Ouvrir", "folder") }));
    }

    public override Task InvokeAsync(string action, CellContext ctx, CancellationToken ct)
    {
        if (action == "open") Actions.ActionRunner.Open((ctx.Str("drive") ?? "C:") + "\\");
        return Task.CompletedTask;
    }
}

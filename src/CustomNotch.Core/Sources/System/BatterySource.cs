using CustomNotch.Core.Model;
using CustomNotch.Core.Platform;

namespace CustomNotch.Core.Sources.System;

public sealed class BatterySource : SourceBase
{
    public override string Type => "system.battery";
    public override SourceSchema Schema => new(Type, "Batterie", Array.Empty<SchemaField>(), "battery");
    public override TimeSpan DefaultRefresh => TimeSpan.FromSeconds(30);

    public override Task<Reading> ReadAsync(CellContext ctx, CancellationToken ct)
    {
        var power = SystemInfo.Power() ?? throw new InvalidOperationException("Alimentation indisponible sur ce système.");
        if (!power.HasBattery)
            return Task.FromResult(new Reading(Status: Status.Off, Text: "Secteur", Detail: new[] { new DetailRow("Batterie", "aucune") }));
        var thresholds = new Thresholds(20, 10, Invert: true);
        var status = power.OnAc ? Status.Ok : thresholds.Judge(power.Percent);
        var left = power.SecondsLeft > 0 ? $"{power.SecondsLeft / 3600} h {(power.SecondsLeft % 3600) / 60:00}" : null;
        return Task.FromResult(new Reading(Value: power.Percent, Max: 100, Unit: "%", Status: status,
            Detail: new[]
            {
                new DetailRow("Charge", $"{power.Percent} %", power.Percent / 100.0, power.OnAc ? "sur secteur" : left is null ? null : $"reste {left}"),
            }));
    }
}

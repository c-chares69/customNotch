using System.Net.NetworkInformation;
using CustomNotch.Core.Model;

namespace CustomNotch.Core.Sources.System;

public sealed class NetworkSource : SourceBase
{
    private (long Ms, ulong Rx, ulong Tx)? _previous;
    private readonly Queue<(long, double)> _history = new();

    public override string Type => "system.network";
    public override SourceSchema Schema => new(Type, "Réseau", new[] { new SchemaField("iface", "string", "Interface", Help: "Vide = toutes") }, "network");
    public override TimeSpan DefaultRefresh => TimeSpan.FromSeconds(2);

    public override Task<Reading> ReadAsync(CellContext ctx, CancellationToken ct)
    {
        var wanted = ctx.Str("iface");
        ulong rx = 0, tx = 0;
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback || nic.OperationalStatus != OperationalStatus.Up) continue;
            if (wanted is not null && !nic.Name.Contains(wanted, StringComparison.OrdinalIgnoreCase)) continue;
            var stats = nic.GetIPStatistics();
            rx += (ulong)stats.BytesReceived;
            tx += (ulong)stats.BytesSent;
        }
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var (down, up) = (0.0, 0.0);
        var unit = "Ko/s";
        if (_previous is { } p && now > p.Ms)
        {
            var seconds = (now - p.Ms) / 1000.0;
            var d = Units.Rate(rx >= p.Rx ? rx - p.Rx : 0, seconds);
            var u = Units.Rate(tx >= p.Tx ? tx - p.Tx : 0, seconds);
            var total = Units.Rate((rx >= p.Rx ? rx - p.Rx : 0) + (tx >= p.Tx ? tx - p.Tx : 0), seconds);
            (down, up, unit) = (d.Value, u.Value, total.Unit);
            History.Keep(_history, total.Value * (total.Unit == "Mo/s" ? 1024 : 1), 60);
            _previous = (now, rx, tx);
            return Task.FromResult(new Reading(Value: total.Value, Unit: unit,
                Detail: new[] { new DetailRow("↓ Réception", $"{d.Value} {d.Unit}"), new DetailRow("↑ Émission", $"{u.Value} {u.Unit}") },
                History: _history.ToList()));
        }
        _previous = (now, rx, tx);
        return Task.FromResult(new Reading(Value: 0, Unit: unit, History: _history.ToList()));
    }
}

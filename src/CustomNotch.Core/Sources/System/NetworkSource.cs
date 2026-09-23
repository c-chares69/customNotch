using System.Net.NetworkInformation;
using CustomNotch.Core.Model;

namespace CustomNotch.Core.Sources.System;

public sealed class NetworkSource : SourceBase
{
    private sealed class State
    {
        public (long Ms, ulong Rx, ulong Tx)? Previous;
        public readonly Queue<(long, double)> History = new();
    }

    // Une instance de la source sert toutes les cellules de ce type : l'état (delta réseau, historique)
    // doit donc être gardé par cellule, pas dans des champs d'instance partagés entre elles.
    private readonly Dictionary<string, State> _cells = new();
    private readonly object _lock = new();

    public override string Type => "system.network";
    public override SourceSchema Schema => new(Type, "Réseau", new[] { new SchemaField("iface", "string", "Interface", Help: "Vide = toutes") }, "network", "Débit réseau, avec historique");
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
        lock (_lock)
        {
            if (!_cells.TryGetValue(ctx.CellId, out var state)) _cells[ctx.CellId] = state = new State();
            if (state.Previous is { } p && now > p.Ms)
            {
                var seconds = (now - p.Ms) / 1000.0;
                var deltaRx = rx >= p.Rx ? rx - p.Rx : 0;
                var deltaTx = tx >= p.Tx ? tx - p.Tx : 0;
                var d = Units.Rate(deltaRx, seconds);
                var u = Units.Rate(deltaTx, seconds);
                var total = Units.Rate(deltaRx + deltaTx, seconds);
                History.Keep(state.History, total.Value * (total.Unit == "Mo/s" ? 1024 : 1), 60);
                state.Previous = (now, rx, tx);
                return Task.FromResult(new Reading(Value: total.Value, Unit: total.Unit,
                    Detail: new[] { new DetailRow("↓ Réception", $"{d.Value} {d.Unit}"), new DetailRow("↑ Émission", $"{u.Value} {u.Unit}") },
                    History: state.History.ToList()));
            }
            state.Previous = (now, rx, tx);
            return Task.FromResult(new Reading(Value: 0, Unit: "Ko/s", History: state.History.ToList()));
        }
    }
}

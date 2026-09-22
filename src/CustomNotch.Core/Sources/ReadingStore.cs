using System.Collections.Concurrent;
using CustomNotch.Core.Model;

namespace CustomNotch.Core.Sources;

/// <summary>La dernière lecture de chaque cellule. Changed est levé sur le thread de la source : l'interface repasse par
/// son Dispatcher.</summary>
public sealed class ReadingStore
{
    private readonly ConcurrentDictionary<string, Reading> _readings = new();
    public event Action<string>? Changed;

    public Reading? Get(string cellId) => _readings.GetValueOrDefault(cellId);

    public void Set(string cellId, Reading reading)
    {
        _readings[cellId] = reading;
        Changed?.Invoke(cellId);
    }

    public void Remove(string cellId) => _readings.TryRemove(cellId, out _);
}

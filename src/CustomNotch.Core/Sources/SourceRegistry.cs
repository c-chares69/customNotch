namespace CustomNotch.Core.Sources;

public sealed class SourceRegistry
{
    private readonly Dictionary<string, ISource> _sources = new(StringComparer.Ordinal);

    public void Register(ISource source) => _sources[source.Type] = source;
    public ISource? Get(string type) => _sources.GetValueOrDefault(type);
    public IReadOnlySet<string> Types => _sources.Keys.ToHashSet();
    public IEnumerable<ISource> All => _sources.Values;

    /// <summary>Les schémas par type : ce que la validation et la fenêtre de réglages consomment.</summary>
    public IReadOnlyDictionary<string, SourceSchema> Schemas => _sources.ToDictionary(kv => kv.Key, kv => kv.Value.Schema);
}

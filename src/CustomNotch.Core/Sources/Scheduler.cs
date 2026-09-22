using System.Text.Json.Nodes;
using CustomNotch.Core.Config;
using CustomNotch.Core.Model;

namespace CustomNotch.Core.Sources;

/// <summary>Une boucle par cellule à sa cadence. Une source qui échoue laisse sa dernière lecture, marquée périmée,
/// et ses lectures s'espacent (×2, plafond 10 min) jusqu'au prochain succès. Une cellule-groupe n'a pas de boucle :
/// ses enfants en ont. À l'inactivité (> 5 min sans souris ni clavier) les cadences sous 30 s passent à 30 s.</summary>
public sealed class Scheduler : IDisposable
{
    private static readonly TimeSpan BackoffCap = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan IdleAfter = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan IdleFloor = TimeSpan.FromSeconds(30);

    private sealed class Loop
    {
        public required CellConfig Cell;
        public required ISource Source;
        public required CellContext Context;
        public required TimeSpan Refresh;
        public readonly CancellationTokenSource Stop = new();
        public readonly SemaphoreSlim Wake = new(0);
        public int Failures;
        public Task? Task;
    }

    private readonly SourceRegistry _registry;
    private readonly ReadingStore _store;
    private readonly Func<long> _now;
    private readonly Func<long> _idleMs;
    private readonly Dictionary<string, Loop> _loops = new();
    private readonly object _lock = new();
    private JsonObject? _globals;

    /// <summary>Toutes les sources doivent être enregistrées avant de construire l'ordonnanceur : c'est ici que
    /// leur événement Pushed est câblé.</summary>
    public Scheduler(SourceRegistry registry, ReadingStore store, Func<long> nowMs, Func<long> idleMs)
    {
        _registry = registry;
        _store = store;
        _now = nowMs;
        _idleMs = idleMs;
        foreach (var source in registry.All) source.Pushed += RefreshNow;
    }

    /// <summary>Met les boucles en accord avec la config : celles dont la cellule a changé (source, params, cadence)
    /// redémarrent, les autres continuent, les disparues s'arrêtent.</summary>
    public void Apply(CellsFile file)
    {
        lock (_lock)
        {
            _globals = file.Sources;
            var wanted = file.AllCells().Where(c => !c.IsGroup).ToDictionary(c => c.Id);
            foreach (var id in _loops.Keys.Where(id => !wanted.ContainsKey(id) || Signature(wanted[id]) != Signature(_loops[id].Cell)).ToList())
            {
                _loops[id].Stop.Cancel();
                _loops.Remove(id);
                if (!wanted.ContainsKey(id)) _store.Remove(id);
            }
            foreach (var cell in wanted.Values.Where(c => !_loops.ContainsKey(c.Id)))
            {
                var source = _registry.Get(cell.Source);
                if (source is null) continue;
                var loop = new Loop
                {
                    Cell = cell, Source = source,
                    Context = new CellContext(cell.Id, cell.Params ?? new JsonObject(), _globals?[cell.Source] as JsonObject),
                    Refresh = cell.RefreshSpan() ?? source.DefaultRefresh,
                };
                loop.Task = Task.Run(() => RunAsync(loop));
                _loops[cell.Id] = loop;
            }
        }
    }

    private static string Signature(CellConfig c) => $"{c.Source}|{c.Refresh}|{c.Params?.ToJsonString()}";

    private async Task RunAsync(Loop loop)
    {
        var ct = loop.Stop.Token;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var reading = await loop.Source.ReadAsync(loop.Context, ct).ConfigureAwait(false);
                    // Une boucle remplacée (Apply : source, params ou cadence changés) est annulée avant que
                    // sa lecture en cours ne revienne. Si la source ignore le jeton, cette lecture périmée ne
                    // doit pas écraser la lecture de sa remplaçante.
                    if (ct.IsCancellationRequested) return;
                    loop.Failures = 0;
                    _store.Set(loop.Cell.Id, reading.Fresh());
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                catch (Exception ex)
                {
                    // Même raison : une panne rapportée après l'annulation ne doit pas écraser la lecture
                    // de la boucle qui a remplacé celle-ci.
                    if (ct.IsCancellationRequested) return;
                    loop.Failures = Math.Min(loop.Failures + 1, 20);
                    var previous = _store.Get(loop.Cell.Id) ?? Reading.Empty;
                    _store.Set(loop.Cell.Id, previous.AsStale(_now(), ex.Message));
                    Log.Warning("source", $"{loop.Cell.Id} ({loop.Source.Type}) : {ex.Message}");
                }
                var wait = loop.Refresh;
                if (loop.Failures > 0)
                {
                    var factor = Math.Pow(2, loop.Failures - 1);
                    wait = TimeSpan.FromMilliseconds(Math.Min(loop.Refresh.TotalMilliseconds * factor, BackoffCap.TotalMilliseconds));
                }
                if (_idleMs() > IdleAfter.TotalMilliseconds && wait < IdleFloor) wait = IdleFloor;
                try { await loop.Wake.WaitAsync(wait, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }
        finally
        {
            // RunAsync est seule propriétaire de ces ressources : elles ne sont plus utiles une fois la
            // boucle arrêtée (Apply l'a remplacée, ou Dispose a tout arrêté).
            loop.Wake.Dispose();
            loop.Stop.Dispose();
        }
    }

    public void RefreshNow(string cellId)
    {
        lock (_lock)
        {
            if (_loops.TryGetValue(cellId, out var loop))
            {
                loop.Failures = 0;
                // La boucle a pu se terminer et disposer Wake entre le TryGetValue et ici.
                try { if (loop.Wake.CurrentCount == 0) loop.Wake.Release(); }
                catch (ObjectDisposedException) { }
            }
        }
    }

    public async Task InvokeAsync(string cellId, string action)
    {
        Loop? loop;
        lock (_lock) _loops.TryGetValue(cellId, out loop);
        if (loop is null) return;
        try
        {
            await loop.Source.InvokeAsync(action, loop.Context, loop.Stop.Token).ConfigureAwait(false);
            RefreshNow(cellId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warning("action", $"{cellId}.{action} : {ex.Message}");
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var loop in _loops.Values) loop.Stop.Cancel();
            _loops.Clear();
        }
        foreach (var source in _registry.All) source.Pushed -= RefreshNow;
    }
}

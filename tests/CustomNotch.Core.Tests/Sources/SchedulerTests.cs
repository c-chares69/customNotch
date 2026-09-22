using System.Text.Json.Nodes;
using CustomNotch.Core.Config;
using CustomNotch.Core.Model;
using CustomNotch.Core.Sources;
using Xunit;
namespace CustomNotch.Core.Tests.Sources;

public class SchedulerTests
{
    private sealed class FakeSource : SourceBase
    {
        public int Reads;
        public Func<Reading>? Produce;
        public string? LastAction;
        public override string Type => "fake";
        public override SourceSchema Schema => new("fake", "Fake", Array.Empty<SchemaField>());
        public override TimeSpan DefaultRefresh => TimeSpan.FromMilliseconds(50);
        public override Task<Reading> ReadAsync(CellContext ctx, CancellationToken ct)
        {
            Interlocked.Increment(ref Reads);
            return Task.FromResult(Produce?.Invoke() ?? new Reading(Value: Reads));
        }
        public override Task InvokeAsync(string action, CellContext ctx, CancellationToken ct) { LastAction = action; return Task.CompletedTask; }
        public void Fire(string id) => Push(id);
    }

    private static CellsFile File(params string[] ids) => new()
    {
        Pills = { new PillConfig { Id = "p", Cells = ids.Select(i => new CellConfig { Id = i, Source = "fake" }).ToList() } },
    };

    private static (Scheduler, FakeSource, ReadingStore) Make(Func<long>? idle = null)
    {
        var registry = new SourceRegistry();
        var source = new FakeSource();
        registry.Register(source);
        var store = new ReadingStore();
        return (new Scheduler(registry, store, () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), idle ?? (() => 0)), source, store);
    }

    [Fact]
    public async Task Chaque_cellule_est_lue_a_sa_cadence()
    {
        var (scheduler, source, store) = Make();
        using (scheduler)
        {
            scheduler.Apply(File("a"));
            await Task.Delay(300);
            Assert.InRange(source.Reads, 3, 10);
            Assert.NotNull(store.Get("a"));
        }
    }

    [Fact]
    public async Task Une_panne_marque_la_derniere_lecture_perimee_sans_la_perdre()
    {
        var (scheduler, source, store) = Make();
        using (scheduler)
        {
            source.Produce = () => new Reading(Value: 42);
            scheduler.Apply(File("a"));
            await Task.Delay(120);
            source.Produce = () => throw new InvalidOperationException("réseau HS");
            await Task.Delay(200);
            var r = store.Get("a")!;
            Assert.Equal(42, r.Value);
            Assert.NotNull(r.StaleSinceMs);
            Assert.Equal("réseau HS", r.Error);
        }
    }

    [Fact]
    public async Task Les_pannes_repetees_espacent_les_lectures()
    {
        var (scheduler, source, _) = Make();
        using (scheduler)
        {
            source.Produce = () => throw new InvalidOperationException("x");
            scheduler.Apply(File("a"));
            await Task.Delay(400);
            // 50 ms, 100, 200, 400… : au plus 4 lectures en 400 ms au lieu de 8
            Assert.InRange(source.Reads, 2, 5);
        }
    }

    [Fact]
    public async Task Une_cellule_retiree_de_la_config_s_arrete()
    {
        var (scheduler, source, store) = Make();
        using (scheduler)
        {
            scheduler.Apply(File("a", "b"));
            await Task.Delay(120);
            scheduler.Apply(File("a"));
            var reads = source.Reads;
            await Task.Delay(150);
            Assert.Null(store.Get("b"));
            Assert.True(source.Reads > reads, "a continue");
        }
    }

    [Fact]
    public async Task Push_et_RefreshNow_relisent_sans_attendre()
    {
        var (scheduler, source, _) = Make();
        using (scheduler)
        {
            scheduler.Apply(new CellsFile { Pills = { new PillConfig { Id = "p", Cells = { new CellConfig { Id = "a", Source = "fake", Refresh = "1h" } } } } });
            await Task.Delay(100);
            Assert.Equal(1, source.Reads);
            source.Fire("a");
            await Task.Delay(100);
            Assert.Equal(2, source.Reads);
            scheduler.RefreshNow("a");
            await Task.Delay(100);
            Assert.Equal(3, source.Reads);
        }
    }

    [Fact]
    public async Task Inactif_les_cadences_courtes_passent_a_30_s()
    {
        var (scheduler, source, _) = Make(idle: () => 10 * 60_000);
        using (scheduler)
        {
            scheduler.Apply(File("a"));
            await Task.Delay(250);
            Assert.Equal(1, source.Reads);
        }
    }

    [Fact]
    public async Task Invoke_atteint_la_source_avec_les_params_de_la_cellule()
    {
        var (scheduler, source, _) = Make();
        using (scheduler)
        {
            scheduler.Apply(File("a"));
            await scheduler.InvokeAsync("a", "toggle");
            Assert.Equal("toggle", source.LastAction);
        }
    }
}

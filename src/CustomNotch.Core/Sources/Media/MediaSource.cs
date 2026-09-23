using CustomNotch.Core.Model;

namespace CustomNotch.Core.Sources.Media;

/// <summary>La cellule « ce qui joue » : titre — artiste, occupée en lecture, boutons précédent / lecture-pause / suivant.
/// Une seule instance sert toutes les cellules média : elle retient leurs ids pour les pousser au changement.</summary>
public sealed class MediaSource : SourceBase
{
    private readonly IMediaSession? _session;
    private readonly HashSet<string> _cells = new();
    private readonly object _lock = new();

    public MediaSource(IMediaSession? session)
    {
        _session = session;
        if (session is not null) session.Changed += () =>
        {
            string[] ids;
            lock (_lock) ids = _cells.ToArray();
            foreach (var id in ids) Push(id);
        };
    }

    public override string Type => "media";
    public override SourceSchema Schema => new(Type, "Média", Array.Empty<SchemaField>(), "music",
        "Ce qui joue (Spotify, navigateur, VLC…) : titre, lecture/pause, piste suivante", "toggle");
    public override TimeSpan DefaultRefresh => TimeSpan.FromSeconds(5);

    public override Task<Reading> ReadAsync(CellContext ctx, CancellationToken ct)
    {
        lock (_lock) _cells.Add(ctx.CellId);
        var state = _session?.Current();
        if (state is null)
            return Task.FromResult(new Reading(Text: "Aucune lecture", Status: Status.Off, Detail: new[] { new DetailRow("Lecture", "aucune session média") }));
        var text = state.Artist.Length > 0 ? $"{state.Title} — {state.Artist}" : state.Title;
        return Task.FromResult(new Reading(Text: text, Status: state.Playing ? Status.Busy : Status.Ok,
            Detail: new[]
            {
                new DetailRow("Titre", state.Title), new DetailRow("Artiste", state.Artist.Length > 0 ? state.Artist : "—"),
                new DetailRow("Application", state.App),
            },
            Actions: new[]
            {
                new ActionSpec("prev", "Précédent", "prev"), new ActionSpec("toggle", state.Playing ? "Pause" : "Lecture", state.Playing ? "pause" : "play"),
                new ActionSpec("next", "Suivant", "next"),
            },
            Image: state.Cover));
    }

    public override Task InvokeAsync(string action, CellContext ctx, CancellationToken ct)
    {
        if (_session is null) return Task.CompletedTask;
        return action switch
        {
            "toggle" => _session.ToggleAsync(),
            "next" => _session.NextAsync(),
            "prev" => _session.PreviousAsync(),
            _ => Task.CompletedTask,
        };
    }
}

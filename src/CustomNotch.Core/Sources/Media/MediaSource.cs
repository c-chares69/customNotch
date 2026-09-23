using CustomNotch.Core.Actions;
using CustomNotch.Core.Model;

namespace CustomNotch.Core.Sources.Media;

/// <summary>La cellule « ce qui joue » : titre — artiste, occupée en lecture, boutons précédent / lecture-pause / suivant
/// sans libellé (la carte les montre en icônes seules), position de lecture quand la session la donne. Sans lecture,
/// le clic ouvre <c>fallbackOpen</c> (un lecteur à lancer, « spotify: » par défaut) plutôt que de ne rien faire.
/// Une seule instance sert toutes les cellules média : elle retient leurs ids pour les pousser au changement.</summary>
public sealed class MediaSource : SourceBase
{
    private const string DefaultFallback = "spotify:";
    private readonly IMediaSession? _session;
    private readonly Func<string, bool> _open;
    private readonly HashSet<string> _cells = new();
    private readonly object _lock = new();

    /// <summary><paramref name="open"/> : injectable pour les tests (défaut <see cref="ActionRunner.Open"/>) — ouvre
    /// le repli configuré quand un clic arrive sans session média.</summary>
    public MediaSource(IMediaSession? session, Func<string, bool>? open = null)
    {
        _session = session;
        _open = open ?? ActionRunner.Open;
        if (session is not null) session.Changed += () =>
        {
            string[] ids;
            lock (_lock) ids = _cells.ToArray();
            foreach (var id in ids) Push(id);
        };
    }

    public override string Type => "media";
    public override SourceSchema Schema => new(Type, "Média",
        new[] { new SchemaField("fallbackOpen", "string", "Sans lecture, le clic ouvre", Default: DefaultFallback) },
        "music", "Ce qui joue (Spotify, navigateur, VLC…) : titre, lecture/pause, piste suivante", "toggle",
        DefaultCaption: false);
    public override TimeSpan DefaultRefresh => TimeSpan.FromSeconds(5);

    /// <summary>« 2:31 » : minutes sans zéro devant, secondes sur deux chiffres.</summary>
    private static string Clock(long ms) => $"{ms / 60000}:{ms / 1000 % 60:00}";

    /// <summary>Un nom lisible pour la cible du clic sans lecture : « spotify: » → « Spotify » (le nom du schéma,
    /// majuscule initiale, sans le « : ») ; une URL → son hôte (« open.spotify.com ») ; un chemin de fichier → son
    /// nom de fichier. Jamais d'exception sur une cible mal formée — au pire, la cible telle quelle.</summary>
    private static string AppLabel(string target)
    {
        var scheme = target.IndexOf("://", StringComparison.Ordinal);
        if (scheme < 0)
        {
            var colon = target.IndexOf(':');
            if (colon > 0 && colon == target.Length - 1)
                return char.ToUpperInvariant(target[0]) + target[1..colon];
            return FileName(target);
        }
        var host = target[(scheme + 3)..].Split('/', 2)[0];
        return host.Length > 0 ? host : FileName(target);
    }

    private static string FileName(string target)
    {
        var trimmed = target.TrimEnd('/', '\\');
        var i = trimmed.LastIndexOfAny(new[] { '/', '\\' });
        return i >= 0 && i + 1 < trimmed.Length ? trimmed[(i + 1)..] : trimmed;
    }

    public override Task<Reading> ReadAsync(CellContext ctx, CancellationToken ct)
    {
        lock (_lock) _cells.Add(ctx.CellId);
        var state = _session?.Current();
        if (state is null)
        {
            var fallbackOpen = ctx.Str("fallbackOpen") ?? DefaultFallback;
            return Task.FromResult(new Reading(Text: "Aucune lecture", Status: Status.Off,
                Detail: new[] { new DetailRow("Lecture", $"aucune — cliquer ouvre {AppLabel(fallbackOpen)}") },
                Actions: new[] { new ActionSpec("open", "Ouvrir", "open") }));
        }
        var text = state.Artist.Length > 0 ? $"{state.Title} — {state.Artist}" : state.Title;
        // La position va dans Detail sous une forme dédiée (label « position », Hint « timeline:… ») : elle ne
        // doit jamais faire un anneau sur la pilule (Value/Max resteraient null), seule la carte la lit.
        var detail = state.PositionMs is { } pos && state.DurationMs is { } dur and > 0
            ? new[] { new DetailRow("position", $"{Clock(pos)} / {Clock(dur)}", (double)pos / dur, $"timeline:{pos}:{dur}:{state.PositionAtMs}") }
            : Array.Empty<DetailRow>();
        return Task.FromResult(new Reading(Text: text, Status: state.Playing ? Status.Busy : Status.Ok,
            Detail: detail,
            Actions: new[]
            {
                new ActionSpec("prev", "", "prev"), new ActionSpec("toggle", "", state.Playing ? "pause" : "play"),
                new ActionSpec("next", "", "next"),
            },
            Image: state.Cover));
    }

    public override Task InvokeAsync(string action, CellContext ctx, CancellationToken ct)
    {
        // « Sans session » = rien à piloter : pas d'implémentation, ou une implémentation sans lecture en cours.
        if (_session is null || _session.Current() is null)
        {
            if (action is "toggle" or "open") _open(ctx.Str("fallbackOpen") ?? DefaultFallback);
            return Task.CompletedTask;
        }
        return action switch
        {
            "toggle" => _session.ToggleAsync(),
            "next" => _session.NextAsync(),
            "prev" => _session.PreviousAsync(),
            _ => Task.CompletedTask,
        };
    }
}

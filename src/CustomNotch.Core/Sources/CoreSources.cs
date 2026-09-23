using CustomNotch.Core.Sources.Claude;
using CustomNotch.Core.Sources.Media;

namespace CustomNotch.Core.Sources;

/// <summary>Toutes les sources livrées avec le cœur, dans un registre neuf. La session média est fournie par
/// l'application (WinRT) ; null = la source média se dit hors service. <paramref name="claude"/> vient du
/// Controller (jeton, HTTP, CLI trouvé par <c>ClaudeCli</c>, registre des sessions) : l'App fournit toujours
/// l'instance réelle. <c>null</c> (tests, catalogue de schémas) retombe sur une instance à vide
/// (<see cref="StubClaude"/>) : le type <c>claude</c> reste connu de la validation et du formulaire de la
/// fenêtre Réglages sans toucher au disque ni au réseau, sa lecture n'étant simplement jamais sollicitée.</summary>
public static class CoreSources
{
    public static SourceRegistry Build(IMediaSession? media = null, ClaudeSource? claude = null)
    {
        var registry = new SourceRegistry();
        System.SystemSources.RegisterAll(registry);
        registry.Register(new LauncherSource());
        registry.Register(new HttpSource());
        registry.Register(new ShellSource());
        registry.Register(new MediaSource(media));
        registry.Register(claude ?? StubClaude());
        return registry;
    }

    /// <summary>Ni fichier ni réseau : chaque délégué rend systématiquement « rien » (pas de jeton, pas de CLI,
    /// pas de pid vivant). Sert uniquement à ce que <c>Schemas</c>/<c>Types</c> connaissent le type <c>claude</c>
    /// (tests de configuration, catalogue) - jamais construite par le Controller, qui passe toujours la vraie.</summary>
    private static ClaudeSource StubClaude()
    {
        long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var renewal = new TokenRenewal(() => null, () => null, _ => Task.FromResult(-1), Now);
        var sessions = new SessionRegistry("", _ => null, Now);
        return new ClaudeSource(_ => null, new UsageClient(HttpSource.Http, Now), renewal, sessions, "", Now);
    }
}

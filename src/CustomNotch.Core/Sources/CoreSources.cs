using CustomNotch.Core.Sources.Claude;
using CustomNotch.Core.Sources.Media;

namespace CustomNotch.Core.Sources;

/// <summary>Toutes les sources livrées avec le cœur, dans un registre neuf. La session média est fournie par
/// l'application (WinRT) ; null = la source média se dit hors service. <paramref name="claude"/> vient de
/// l'App (jeton, HTTP, CLI, registre des sessions — plan 4) ; null (tests, avant ce câblage) retombe sur une
/// instance sans délégué réel : le type <c>claude</c> reste connu de la validation et du catalogue, sa lecture
/// n'est simplement jamais sollicitée tant que le contrôleur n'en construit pas une vraie.</summary>
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
        registry.Register(claude ?? DefaultClaude());
        return registry;
    }

    private static ClaudeSource DefaultClaude()
    {
        long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var dir = ClaudeCredentialsFile.DefaultDir();
        var usage = new UsageClient(HttpSource.Http, Now);
        // Sans CLI ni sans façon de vérifier qu'un pid est vivant (délégués posés par l'App, plan 4), le
        // renouvellement échoue simplement et le registre des sessions ne montre jamais rien - jamais
        // d'exception, une cellule qui se dit juste hors service tant que le vrai câblage n'est pas en place.
        var renewal = new TokenRenewal(() => ClaudeCredentialsFile.Read(dir), () => null, _ => Task.FromResult(-1), Now);
        var sessions = new SessionRegistry(Path.Combine(dir, "sessions"), _ => null, Now);
        var backoffPath = Path.Combine(dir, "claude-backoff.json");
        return new ClaudeSource(ClaudeCredentialsFile.Read, usage, renewal, sessions, backoffPath, Now);
    }
}

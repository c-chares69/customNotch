using CustomNotch.Core.Sources.Media;

namespace CustomNotch.Core.Sources;

/// <summary>Toutes les sources livrées avec le cœur, dans un registre neuf. La session média est fournie par
/// l'application (WinRT) ; null = la source média se dit hors service.</summary>
public static class CoreSources
{
    public static SourceRegistry Build(IMediaSession? media = null)
    {
        var registry = new SourceRegistry();
        System.SystemSources.RegisterAll(registry);
        registry.Register(new LauncherSource());
        registry.Register(new HttpSource());
        registry.Register(new ShellSource());
        registry.Register(new MediaSource(media));
        return registry;
    }
}

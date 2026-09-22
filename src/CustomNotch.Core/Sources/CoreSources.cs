namespace CustomNotch.Core.Sources;

/// <summary>Toutes les sources livrées avec le cœur, dans un registre neuf.</summary>
public static class CoreSources
{
    public static SourceRegistry Build()
    {
        var registry = new SourceRegistry();
        System.SystemSources.RegisterAll(registry);
        registry.Register(new LauncherSource());
        registry.Register(new HttpSource());
        registry.Register(new ShellSource());
        return registry;
    }
}

namespace CustomNotch.Core.Sources.System;

public static class SystemSources
{
    public static void RegisterAll(SourceRegistry registry)
    {
        registry.Register(new CpuSource());
        registry.Register(new MemorySource());
        registry.Register(new DiskSource());
        registry.Register(new NetworkSource());
        registry.Register(new BatterySource());
    }
}

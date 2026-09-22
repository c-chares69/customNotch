namespace CustomNotch.Core.Sources.System;

public static class Units
{
    public static double Gb(ulong bytes) => Math.Round(bytes / 1_073_741_824.0, 1);

    /// <summary>Un débit lisible : Ko/s sous 1 Mo/s, Mo/s au-dessus.</summary>
    public static (double Value, string Unit) Rate(ulong deltaBytes, double seconds)
    {
        if (seconds <= 0) return (0, "Ko/s");
        var perSecond = deltaBytes / seconds;
        return perSecond >= 1024 * 1024 ? (Math.Round(perSecond / (1024 * 1024), 1), "Mo/s") : (Math.Round(perSecond / 1024, 0), "Ko/s");
    }

    /// <summary>GetSystemTimes : kernel contient idle ; l'occupation est 1 − Δidle / (Δkernel + Δuser).</summary>
    public static double CpuPercent((ulong Idle, ulong Kernel, ulong User) prev, (ulong Idle, ulong Kernel, ulong User) cur)
    {
        var total = (double)(cur.Kernel - prev.Kernel) + (cur.User - prev.User);
        if (total <= 0) return 0;
        var idle = (double)(cur.Idle - prev.Idle);
        return Math.Clamp((1 - idle / total) * 100, 0, 100);
    }
}

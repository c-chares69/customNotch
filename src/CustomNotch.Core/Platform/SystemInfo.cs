using System.Runtime.InteropServices;

namespace CustomNotch.Core.Platform;

/// <summary>Les trois lectures système que Windows donne sans compteur de performance ni WMI : temps CPU, mémoire,
/// alimentation. Hors Windows chaque fonction rend null et la source se dit indisponible.</summary>
public static partial class SystemInfo
{
    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime { public uint Low, High; public ulong Value => ((ulong)High << 32) | Low; }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx status);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte AcLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
        public int BatteryLifeTime, BatteryFullLifeTime;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSystemPowerStatus(out SystemPowerStatus status);

    public static (ulong Idle, ulong Kernel, ulong User)? CpuTimes()
    {
        if (!OperatingSystem.IsWindows() || !GetSystemTimes(out var idle, out var kernel, out var user)) return null;
        return (idle.Value, kernel.Value, user.Value);
    }

    public static (ulong Total, ulong Available)? Memory()
    {
        if (!OperatingSystem.IsWindows()) return null;
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        return GlobalMemoryStatusEx(ref status) ? (status.TotalPhys, status.AvailPhys) : null;
    }

    /// <summary>BatteryFlag 128 = pas de batterie ; BatteryLifePercent 255 = inconnu ; BatteryLifeTime −1 = inconnu.</summary>
    public static (bool HasBattery, bool OnAc, int Percent, int SecondsLeft)? Power()
    {
        if (!OperatingSystem.IsWindows() || !GetSystemPowerStatus(out var s)) return null;
        var has = (s.BatteryFlag & 128) == 0 && s.BatteryLifePercent != 255;
        return (has, s.AcLineStatus == 1, s.BatteryLifePercent == 255 ? 0 : s.BatteryLifePercent, s.BatteryLifeTime);
    }
}

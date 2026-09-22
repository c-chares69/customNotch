using System.Runtime.InteropServices;

namespace CustomNotch.Core.Platform;

/// <summary>Détection d'inactivité : évite de facturer une pause café au client.</summary>
public static partial class Idle
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint cbSize;
        public uint dwTime;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetLastInputInfo(ref LastInputInfo info);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetTickCount();

    /// <summary>Millisecondes écoulées depuis la dernière frappe ou le dernier mouvement souris.</summary>
    public static long Ms()
    {
        if (!OperatingSystem.IsWindows()) return 0;
        var info = new LastInputInfo { cbSize = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info)) return 0;
        // GetTickCount et dwTime partagent la même base ; la soustraction 32 bits gère le rebouclage.
        return unchecked(GetTickCount() - info.dwTime);
    }
}

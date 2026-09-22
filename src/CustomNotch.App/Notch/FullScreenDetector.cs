using System.Runtime.InteropServices;

namespace CustomNotch.App.Notch;

/// <summary>La fenêtre au premier plan couvre-t-elle l'écran entier ? Un jeu ou une vidéo en plein écran ne veut pas
/// d'une pilule par-dessus. Le bureau et le shell (Progman, WorkerW) ne comptent pas : ils couvrent toujours tout.</summary>
public static partial class FullScreenDetector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(nint hwnd, out NativeRect rect);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint hwnd, System.Text.StringBuilder name, int max);

    public static bool IsFullScreenOn(System.Windows.Forms.Screen screen)
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == 0 || !GetWindowRect(hwnd, out var r)) return false;
        var name = new System.Text.StringBuilder(64);
        GetClassName(hwnd, name, name.Capacity);
        if (name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd") return false;
        var b = screen.Bounds;
        return r.Left <= b.Left && r.Top <= b.Top && r.Right >= b.Right && r.Bottom >= b.Bottom;
    }
}

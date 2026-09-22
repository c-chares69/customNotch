using System.Runtime.InteropServices;

namespace CustomNotch.App;

/// <summary>Les quelques appels Win32 des surfaces.</summary>
internal static class Native
{
    public const int GwlExStyle = -20;
    public const int WsExToolWindow = 0x00000080;
    public const int WsExNoActivate = 0x08000000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    public static extern int GetWindowLong(nint hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    public static extern int SetWindowLong(nint hwnd, int index, int value);
}

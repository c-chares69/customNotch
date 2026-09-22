using System.Globalization;
using System.IO;
using System.Windows;
using Application = System.Windows.Application;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using CustomNotch.Core;
using CustomNotch.Core.Config;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace CustomNotch.App;

/// <summary>Les couleurs de la version Python, et leur pose dans les ressources WPF.</summary>
public static class Theme
{
    public sealed record Palette(Color Bg, Color Surface, Color SurfaceHover, Color Fg, Color Muted, Color Subtle, Color Track,
                                 Color Border, Color Danger, Color Success, Color Warn, Color OnAccent);

    // Fond profond, surfaces à peine plus claires, bordure translucide : les cartes se
    // détachent par la matière plutôt que par l'ombre.
    public static readonly Palette Dark = new(
        Rgb(15, 17, 22), Rgb(23, 26, 34), Rgb(31, 35, 46), Rgb(237, 239, 245), Rgb(154, 161, 180), Rgb(107, 114, 133),
        Rgb(38, 43, 55), Color.FromArgb(18, 255, 255, 255), Rgb(240, 101, 94), Rgb(52, 199, 143), Rgb(245, 185, 74), Rgb(255, 255, 255));

    public static readonly Palette Light = new(
        Rgb(246, 247, 250), Rgb(255, 255, 255), Rgb(239, 241, 246), Rgb(23, 25, 33), Rgb(96, 103, 120), Rgb(138, 144, 160),
        Rgb(226, 229, 236), Color.FromArgb(22, 16, 18, 27), Rgb(206, 44, 49), Rgb(13, 148, 100), Rgb(178, 116, 12), Rgb(255, 255, 255));

    private static Color Rgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

    /// <summary>« dark » ou « light » selon le réglage d'apparence de Windows.</summary>
    public static string SystemScheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v != 0 ? "light" : "dark";
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException)
        {
            return "dark";
        }
    }

    /// <summary>Couleur d'accent choisie dans les réglages Windows.</summary>
    public static Color? SystemAccent()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key?.GetValue("AccentColor") is int abgr)
            {
                var v = unchecked((uint)abgr);
                return Color.FromRgb((byte)(v & 0xFF), (byte)((v >> 8) & 0xFF), (byte)((v >> 16) & 0xFF));
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException) { }
        return null;
    }

    public static string ResolveTheme(AppConfig cfg)
    {
        var chosen = cfg.GetString("appearance.theme", "system");
        return chosen == "system" || chosen.Length == 0 ? SystemScheme() : chosen;
    }

    public static Color ResolveAccent(AppConfig cfg)
    {
        if (cfg.GetString("appearance.accent_source", "system") != "custom" && SystemAccent() is { } system) return system;
        return ParseColor(cfg.GetString("appearance.accent", Core.App.DefaultAccent)) ?? ParseColor(Core.App.DefaultAccent)!.Value;
    }

    public static Color? ParseColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        try { return (Color)ColorConverter.ConvertFromString(hex.Trim()); }
        catch (FormatException) { return null; }
    }

    public static Palette PaletteFor(string theme) => theme == "light" ? Light : Dark;

    /// <summary>Interpole deux couleurs (ratio = part de b).</summary>
    public static Color Mix(Color a, Color b, double ratio)
    {
        ratio = Math.Clamp(ratio, 0, 1);
        return Color.FromArgb(255, (byte)Math.Round(a.R * (1 - ratio) + b.R * ratio), (byte)Math.Round(a.G * (1 - ratio) + b.G * ratio),
                              (byte)Math.Round(a.B * (1 - ratio) + b.B * ratio));
    }

    public static Color WithAlpha(Color c, byte alpha) => Color.FromArgb(alpha, c.R, c.G, c.B);

    /// <summary>L'accent posé par le dernier <see cref="Apply"/> - pour l'icône de la zone de
    /// notification, sans relire le registre à chaque seconde.</summary>
    public static Color CurrentAccent { get; private set; } = ParseColor(Core.App.DefaultAccent)!.Value;

    /// <summary>Pose la palette et l'accent dans les ressources de l'application : toutes les
    /// surfaces liées par DynamicResource se recolorent d'un coup.</summary>
    public static void Apply(Application app, AppConfig cfg)
    {
        var p = PaletteFor(ResolveTheme(cfg));
        var accent = ResolveAccent(cfg);
        CurrentAccent = accent;
        void Set(string key, Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            app.Resources[key] = brush;
        }
        Set("Bg", p.Bg); Set("Surface", p.Surface); Set("SurfaceHover", p.SurfaceHover); Set("Fg", p.Fg); Set("Muted", p.Muted);
        Set("Subtle", p.Subtle); Set("Track", p.Track); Set("Border", p.Border); Set("Danger", p.Danger); Set("Success", p.Success);
        Set("Warn", p.Warn); Set("Accent", accent); Set("OnAccent", p.OnAccent);
        // Dérivés : la teinte d'accent des sélections, et la barre latérale un cran plus sombre
        // (ou, en clair, la surface) pour asseoir la navigation.
        Set("AccentSoft", WithAlpha(accent, (byte)(p == Dark ? 46 : 34)));
        Set("Sidebar", p == Dark ? Mix(p.Bg, Colors.Black, 0.28) : p.Surface);
        CurrentPalette = p;
        foreach (Window window in app.Windows) ApplyChrome(window);
    }

    /// <summary>La palette posée par le dernier <see cref="Apply"/>.</summary>
    public static Palette CurrentPalette { get; private set; } = Dark;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>La barre de titre d'une fenêtre à cadre standard suit la palette : WPF la
    /// laisse blanche par défaut, quel que soit le thème de Windows. Demandé au gestionnaire
    /// de fenêtres (mode sombre immersif, puis couleurs exactes sur Windows 11 ; un système
    /// plus ancien ignore ce qu'il ne connaît pas). À appeler une fois la fenêtre créée
    /// (SourceInitialized) ; <see cref="Apply"/> repasse sur les fenêtres ouvertes.</summary>
    public static void ApplyChrome(Window window)
    {
        if (window.WindowStyle == WindowStyle.None) return;
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        var p = CurrentPalette;
        var dark = p == Dark ? 1 : 0;
        const int UseImmersiveDarkMode = 20, UseImmersiveDarkModeBefore20H1 = 19, CaptionColor = 35, TextColor = 36;
        try
        {
            if (DwmSetWindowAttribute(handle, UseImmersiveDarkMode, ref dark, sizeof(int)) != 0)
                DwmSetWindowAttribute(handle, UseImmersiveDarkModeBefore20H1, ref dark, sizeof(int));
            var caption = ColorRef(p.Bg);
            var text = ColorRef(p.Fg);
            DwmSetWindowAttribute(handle, CaptionColor, ref caption, sizeof(int));
            DwmSetWindowAttribute(handle, TextColor, ref text, sizeof(int));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
    }

    /// <summary>COLORREF : 0x00BBGGRR.</summary>
    private static int ColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

    public static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    /// <summary>Compteur d'icône, court et non ambigu : « 45 », « 3h ».</summary>
    public static string TrayLabel(long ms)
    {
        var minutes = Math.Max(0, ms) / 60_000;
        return minutes < 60 ? minutes.ToString(CultureInfo.InvariantCulture) : $"{Math.Min(minutes / 60, 99)}h";
    }
}

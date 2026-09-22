using System.Windows;
using System.Windows.Media;
using Point = System.Windows.Point;

namespace CustomNotch.App;

/// <summary>Écrans et conversions pixels physiques -> unités WPF, pour placer les
/// surfaces sans bordure là où il faut.</summary>
internal static class Screens
{
    public static (double X, double Y) Scale(Visual visual)
    {
        var source = PresentationSource.FromVisual(visual);
        if (source?.CompositionTarget is null) return (1, 1);
        var m = source.CompositionTarget.TransformToDevice;
        return (m.M11, m.M22);
    }

    private static System.Windows.Forms.Screen ScreenAt(System.Drawing.Point devicePoint)
        => System.Windows.Forms.Screen.FromPoint(devicePoint);

    /// <summary>La zone de travail (sans la barre des tâches) de l'écran qui contient ce point physique, en unités WPF.</summary>
    public static Rect WorkArea(Visual visual, System.Drawing.Point devicePoint)
    {
        var (sx, sy) = Scale(visual);
        var a = ScreenAt(devicePoint).WorkingArea;
        return new Rect(a.Left / sx, a.Top / sy, a.Width / sx, a.Height / sy);
    }

    public static Rect FullArea(Visual visual, System.Drawing.Point devicePoint)
    {
        var (sx, sy) = Scale(visual);
        var b = ScreenAt(devicePoint).Bounds;
        return new Rect(b.Left / sx, b.Top / sy, b.Width / sx, b.Height / sy);
    }

    public static Rect ToDip(Visual visual, System.Drawing.Rectangle device)
    {
        var (sx, sy) = Scale(visual);
        return new Rect(device.Left / sx, device.Top / sy, device.Width / sx, device.Height / sy);
    }

    public static Point CursorDip(Visual visual)
    {
        var (sx, sy) = Scale(visual);
        var p = System.Windows.Forms.Cursor.Position;
        return new Point(p.X / sx, p.Y / sy);
    }
}

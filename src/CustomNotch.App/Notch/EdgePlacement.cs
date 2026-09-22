using System.Windows;

namespace CustomNotch.App.Notch;

/// <summary>Où poser le canvas de la pilule sur un écran : collé au bord demandé, à « along » de sa longueur.
/// Pur : l'écran est un rectangle en DIP, rien de WPF n'est touché.</summary>
public static class EdgePlacement
{
    public static bool IsVertical(string edge) => edge is "left" or "right";

    public static Rect Place(Rect area, string edge, double along, double extent, double thickness)
    {
        along = Math.Clamp(along, 0, 1);
        return edge switch
        {
            "left" => new Rect(area.Left, area.Top + along * (area.Height - extent), thickness, extent),
            "top" => new Rect(area.Left + along * (area.Width - extent), area.Top, extent, thickness),
            "bottom" => new Rect(area.Left + along * (area.Width - extent), area.Bottom - thickness, extent, thickness),
            _ => new Rect(area.Right - thickness, area.Top + along * (area.Height - extent), thickness, extent),
        };
    }

    public static double AlongFrom(Rect area, string edge, Point origin, double extent)
    {
        var room = IsVertical(edge) ? area.Height - extent : area.Width - extent;
        if (room <= 0) return 0;
        var offset = IsVertical(edge) ? origin.Y - area.Top : origin.X - area.Left;
        return Math.Clamp(offset / room, 0, 1);
    }
}

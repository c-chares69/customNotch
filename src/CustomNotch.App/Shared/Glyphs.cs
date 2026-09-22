using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CustomNotch.App;

/// <summary>Les tracés du bouton lecture/pause, analysés une fois et figés.</summary>
internal static class Glyphs
{
    public static readonly Geometry Pause = Frozen("M0,0 L3,0 L3,10 L0,10 Z M6,0 L9,0 L9,10 L6,10 Z");
    public static readonly Geometry Play = Frozen("M0,0 L10,5 L0,10 Z");
    /// <summary>Le triangle décalé d'un point, pour paraître centré dans un petit bouton rond.</summary>
    public static readonly Geometry PlayInset = Frozen("M1,0 L10,5 L1,10 Z");

    /// <summary>Le glyphe seul, à la taille voulue - pour un bouton carré.</summary>
    public static System.Windows.Shapes.Path Icon(bool pause, double size, Brush fill) => Fill(pause ? Pause : Play, size, fill);

    /// <summary>Une icône pleine (lecture, pause), à la taille voulue, centrée.</summary>
    public static System.Windows.Shapes.Path Fill(Geometry geometry, double size, Brush fill) => new()
    {
        Data = geometry, Fill = fill, Width = size, Height = size, Stretch = Stretch.Uniform,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>Le glyphe suivi d'un libellé - pour « Arrêter » / « Démarrer ».</summary>
    public static StackPanel WithLabel(bool pause, string label, Brush fill)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(Icon(pause, 10, fill));
        panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
        return panel;
    }

    // Les icônes d'action, tracées au trait dans une grille 16 x 16 : coche, croix,
    // chevron, crayon, corbeille, horloge. Vectorielles, comme un SVG - jamais un caractère.
    public static readonly Geometry Check = Frozen("M2.5,8.5 L6.5,12.5 L13.5,4");
    public static readonly Geometry Cross = Frozen("M3,3 L13,13 M13,3 L3,13");
    public static readonly Geometry ChevronDown = Frozen("M3,6 L8,11 L13,6");
    public static readonly Geometry Pencil = Frozen("M3,13 L3,10.2 L10.2,3 L13,5.8 L5.8,13 Z M9,4.2 L11.8,7");
    public static readonly Geometry Trash = Frozen("M2.5,4.5 L13.5,4.5 M6,4.5 L6,2.5 L10,2.5 L10,4.5 M4,4.5 L4.8,13.5 L11.2,13.5 L12,4.5 M6.6,7 L6.6,11 M9.4,7 L9.4,11");
    public static readonly Geometry Clock = Frozen("M8,2.5 A5.5,5.5 0 1 1 7.99,2.5 Z M8,4.8 L8,8.2 L10.6,9.6");
    public static readonly Geometry ChevronRight = Frozen("M6,3 L11,8 L6,13");
    public static readonly Geometry Refresh = Frozen("M13,8 A5,5 0 1 1 11.6,4.4 M11.8,2.2 L11.8,4.8 L9.2,4.8");
    public static readonly Geometry Gear = Frozen("M8,5.6 A2.4,2.4 0 1 1 7.99,5.6 Z M8,1.5 L8,3.4 M8,12.6 L8,14.5 M1.5,8 L3.4,8 M12.6,8 L14.5,8 M3.4,3.4 L4.75,4.75 M11.25,11.25 L12.6,12.6 M3.4,12.6 L4.75,11.25 M11.25,4.75 L12.6,3.4");
    public static readonly Geometry OpenWindow = Frozen("M6.5,3 L3,3 L3,13 L13,13 L13,9.5 M9,3 L13,3 L13,7 M13,3 L7.5,8.5");
    public static readonly Geometry Plus = Frozen("M8,3 L8,13 M3,8 L13,8");
    public static readonly Geometry Home = Frozen("M2.5,8 L8,3 L13.5,8 M4,7 L4,13.5 L12,13.5 L12,7");
    public static readonly Geometry Bell = Frozen("M4,11.5 L12,11.5 L11,10 L11,7 A3,3 0 0 0 5,7 L5,10 Z M6.8,13.5 L9.2,13.5");
    public static readonly Geometry Link = Frozen("M6.5,9.5 L9.5,6.5 M7,4.8 L8.4,3.4 A2.6,2.6 0 0 1 12.6,7.6 L11.2,9 M9,11.2 L7.6,12.6 A2.6,2.6 0 0 1 3.4,8.4 L4.8,7");
    public static readonly Geometry Layout = Frozen("M2.5,2.5 L13.5,2.5 L13.5,13.5 L2.5,13.5 Z M2.5,7 L13.5,7 M8,7 L8,13.5");
    public static readonly Geometry Drop = Frozen("M8,2.5 C8,2.5 3.5,7.6 3.5,10.2 A4.5,4.5 0 0 0 12.5,10.2 C12.5,7.6 8,2.5 8,2.5 Z");
    public static readonly Geometry Flag = Frozen("M4,14 L4,2.5 M4,3 L12.5,3 L10.5,6 L12.5,9 L4,9");
    public static readonly Geometry Calendar = Frozen("M2.5,4 L13.5,4 L13.5,13.5 L2.5,13.5 Z M2.5,7 L13.5,7 M5.5,2.5 L5.5,5 M10.5,2.5 L10.5,5");
    public static readonly Geometry Pulse = Frozen("M2,8 L5,8 L6.5,4 L9.5,12 L11,8 L14,8");
    public static readonly Geometry Monitor = Frozen("M2.5,3 L13.5,3 L13.5,10.5 L2.5,10.5 Z M8,10.5 L8,13.5 M5,13.5 L11,13.5");
    // Les icônes des notifications : un genre, une icône. Un point se trace en segment
    // minuscule - les bouts arrondis en font un disque de l'épaisseur du trait.
    public static readonly Geometry Alert = Frozen("M8,2.5 L14,13.5 L2,13.5 Z M8,6.5 L8,9.5 M8,11.6 L8,11.65");
    public static readonly Geometry Info = Frozen("M8,2.5 A5.5,5.5 0 1 1 7.99,2.5 Z M8,7.3 L8,11.2 M8,5 L8,5.05");
    public static readonly Geometry Wifi = Frozen("M2.5,7.5 A7.5,7.5 0 0 1 13.5,7.5 M5,10 A4.5,4.5 0 0 1 11,10 M8,12.6 L8,12.65");
    public static readonly Geometry Keyboard = Frozen("M2,4.5 L14,4.5 L14,11.5 L2,11.5 Z M4.6,7 L4.6,7.05 M6.9,7 L6.9,7.05 M9.1,7 L9.1,7.05 M11.4,7 L11.4,7.05 M5.5,9.5 L10.5,9.5");
    public static readonly Geometry Download = Frozen("M8,2.5 L8,10.5 M4.5,7 L8,10.5 L11.5,7 M3,13.5 L13,13.5");
    public static readonly Geometry Chart = Frozen("M3,13.5 L13,13.5 M5,13.5 L5,8.5 M8,13.5 L8,4.5 M11,13.5 L11,10");
    public static readonly Geometry Moon = Frozen("M14,8.5 A6,6 0 1 1 7.5,2 A4.7,4.7 0 0 0 14,8.5 Z");

    /// <summary>Une icône au trait, à la taille voulue, centrée - pour un bouton ou une cellule.</summary>
    public static System.Windows.Shapes.Path Stroke(Geometry geometry, double size, Brush brush, double thickness = 1.5) => new()
    {
        Data = geometry, Stroke = brush, StrokeThickness = thickness, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
        StrokeLineJoin = PenLineJoin.Round, Width = size, Height = size, Stretch = Stretch.Uniform,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>La même, liée à un pinceau du thème (suit le thème clair / sombre).</summary>
    public static System.Windows.Shapes.Path Stroke(Geometry geometry, double size, string brushKey, double thickness = 1.5)
    {
        var path = Stroke(geometry, size, Brushes.Transparent, thickness);
        path.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, brushKey);
        return path;
    }

    private static Geometry Frozen(string path)
    {
        var g = Geometry.Parse(path);
        g.Freeze();
        return g;
    }
}

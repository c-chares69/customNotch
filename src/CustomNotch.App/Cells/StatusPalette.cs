using System.Windows.Media;
using CustomNotch.Core.Model;

namespace CustomNotch.App.Cells;

/// <summary>Les couleurs de la pilule, fixes quel que soit le thème : c'est son identité. Vert, jaune, rouge-orange
/// comme les anneaux de codenotch ; bleu pour un travail en cours, ambre pour « on t'attend ».</summary>
public static class StatusPalette
{
    public static readonly Color Track = System.Windows.Media.Color.FromRgb(0x30, 0x30, 0x30);
    public static readonly Color Ink = System.Windows.Media.Color.FromRgb(0xe8, 0xe8, 0xea);
    public static readonly Color Dim = System.Windows.Media.Color.FromRgb(0x80, 0x80, 0x80);

    public static Color Color(Status status) => status switch
    {
        Status.Ok => System.Windows.Media.Color.FromRgb(0x30, 0xd1, 0x58),
        Status.Warn => System.Windows.Media.Color.FromRgb(0xff, 0xd6, 0x0a),
        Status.Crit => System.Windows.Media.Color.FromRgb(0xff, 0x45, 0x3a),
        Status.Busy => System.Windows.Media.Color.FromRgb(0x0a, 0x84, 0xff),
        Status.Attention => System.Windows.Media.Color.FromRgb(0xff, 0x9f, 0x0a),
        _ => Dim,
    };

    public static SolidColorBrush Brush(Status status) => Frozen(Color(status));

    public static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}

namespace CustomNotch.App.Notch;

/// <summary>Les dimensions de codenotch, à l'échelle de la pilule. Tout le dessin part d'ici : un seul endroit à
/// changer pour une pilule plus dense.</summary>
public sealed record PillMetrics(double Scale)
{
    public double Width => 70 * Scale;
    public double Corner => 20 * Scale;
    public double Fillet => 38.7 * Scale;
    public double Padding => 18 * Scale;
    public double Ring => 44 * Scale;
    /// <summary>La hauteur réservée à la légende sous l'anneau (15 px de texte + son interligne).</summary>
    public double Caption => 18 * Scale;
    public double Gap => 14 * Scale;
    public double CardWidth => 246;
    public double CardGap => 8;

    /// <summary>La longueur du corps pour n cellules ; une pilule vide garde la place d'une cellule.</summary>
    public double Length(int cells)
    {
        var n = Math.Max(1, cells);
        return 2 * Padding + n * (Ring + Caption) + (n - 1) * Gap;
    }

    public double Extent(int cells) => Length(cells) + 2 * Fillet;
}

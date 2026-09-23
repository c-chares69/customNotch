namespace CustomNotch.App.Notch;

/// <summary>Les dimensions de codenotch, à l'échelle de la pilule. Tout le dessin part d'ici : un seul endroit à
/// changer pour une pilule plus dense. <paramref name="CardScale"/> est indépendante de <paramref name="Scale"/> :
/// elle ne grandit que la carte au survol (réglage global), pas la pilule elle-même.</summary>
public sealed record PillMetrics(double Scale, double CardScale = 1.0)
{
    public double Width => 70 * Scale;
    public double Corner => 20 * Scale;
    public double Fillet => 38.7 * Scale;
    public double Padding => 18 * Scale;
    public double Ring => 44 * Scale;
    /// <summary>La hauteur réservée à la légende sous l'anneau (15 px de texte + son interligne).</summary>
    public double Caption => 18 * Scale;
    public double Gap => 14 * Scale;
    /// <summary>La place réservée côté fenêtre pour la carte (déjà à l'échelle) : la carte elle-même garde une
    /// largeur de base de 340 et se met à l'échelle par LayoutTransform, ce qui revient au même résultat sans
    /// jamais multiplier deux fois.</summary>
    public double CardWidth => 340 * CardScale;
    public double CardGap => 8;

    /// <summary>La longueur du corps pour n cellules ; une pilule vide garde la place d'une cellule.</summary>
    public double Length(int cells)
    {
        var n = Math.Max(1, cells);
        return 2 * Padding + n * (Ring + Caption) + (n - 1) * Gap;
    }

    public double Extent(int cells) => Length(cells) + 2 * Fillet;
}

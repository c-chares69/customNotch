namespace CustomNotch.Core.Model;

/// <summary>Seuils sur la valeur (en % quand la lecture a un maximum, sinon en valeur brute). Invert pour les
/// grandeurs où bas = mauvais (batterie).</summary>
public sealed record Thresholds(double? Warn = null, double? Crit = null, bool Invert = false)
{
    /// <summary>Les seuils d'un anneau quand la config n'en donne pas : jaune à 50 %, rouge à 75 %.</summary>
    public static readonly Thresholds RingDefault = new(50, 75);

    public Status Judge(double value)
    {
        if (Invert)
        {
            if (Crit is { } c && value <= c) return Status.Crit;
            if (Warn is { } w && value <= w) return Status.Warn;
            return Status.Ok;
        }
        if (Crit is { } cc && value >= cc) return Status.Crit;
        if (Warn is { } ww && value >= ww) return Status.Warn;
        return Status.Ok;
    }
}

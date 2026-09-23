namespace CustomNotch.Core.Sources.Media;

/// <summary>Ce qui joue en ce moment, tel que le système le rapporte ; la pochette telle que reçue (PNG/JPEG), ou null.
/// PositionMs/DurationMs/PositionAtMs : la timeline de lecture (null si le système ne la donne pas) — PositionAtMs
/// est l'horodatage (époque, ms) de la mesure, pour que la carte avance la position localement entre deux lectures.
/// L'égalité compare la pochette par contenu : un tableau relu à chaque rafraîchissement ne doit pas faire croire à un
/// changement (Changed serait levé en boucle).</summary>
public sealed record MediaState(string Title, string Artist, string App, bool Playing, byte[]? Cover = null,
    long? PositionMs = null, long? DurationMs = null, long? PositionAtMs = null)
{
    public bool Equals(MediaState? other) =>
        other is not null && Title == other.Title && Artist == other.Artist && App == other.App && Playing == other.Playing
        && PositionMs == other.PositionMs && DurationMs == other.DurationMs && PositionAtMs == other.PositionAtMs
        && ((Cover is null && other.Cover is null) || (Cover is not null && other.Cover is not null && Cover.AsSpan().SequenceEqual(other.Cover)));
    public override int GetHashCode() => HashCode.Combine(Title, Artist, App, Playing, Cover?.Length ?? 0, PositionMs, DurationMs, PositionAtMs);

    /// <summary>Deux états sont « pareils » ici s'ils ne diffèrent que par la position de lecture (PositionMs,
    /// PositionAtMs) — tout le reste (titre, artiste, appli, lecture, pochette, durée) doit être identique.
    /// Contrairement à <see cref="Equals(MediaState?)"/> (qui compare tout, position comprise, et sert aux tests
    /// comme à l'égalité normale des records), ceci sert à décider quand prévenir l'interface : la position avance
    /// sans arrêt tant que ça joue, la reconstituer à chaque tic n'est pas un changement qui mérite de reconstruire
    /// la carte.</summary>
    public static bool SameExceptPosition(MediaState? a, MediaState? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        return a.Title == b.Title && a.Artist == b.Artist && a.App == b.App && a.Playing == b.Playing && a.DurationMs == b.DurationMs
            && ((a.Cover is null && b.Cover is null) || (a.Cover is not null && b.Cover is not null && a.Cover.AsSpan().SequenceEqual(b.Cover)));
    }
}

/// <summary>La session média du système, vue de Core : lire l'état, agir, et prévenir. L'implémentation (WinRT) vit dans
/// l'application ; sans elle — tests, autre OS — la source média se dit hors service.</summary>
public interface IMediaSession
{
    MediaState? Current();
    Task ToggleAsync();
    Task NextAsync();
    Task PreviousAsync();
    /// <summary>Piste, lecture ou session qui change : les cellules abonnées sont relues sans attendre la cadence.</summary>
    event Action? Changed;
}

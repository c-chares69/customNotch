namespace CustomNotch.Core.Sources.Media;

/// <summary>Ce qui joue en ce moment, tel que le système le rapporte ; la pochette telle que reçue (PNG/JPEG), ou null.
/// L'égalité compare la pochette par contenu : un tableau relu à chaque rafraîchissement ne doit pas faire croire à un
/// changement (Changed serait levé en boucle).</summary>
public sealed record MediaState(string Title, string Artist, string App, bool Playing, byte[]? Cover = null)
{
    public bool Equals(MediaState? other) =>
        other is not null && Title == other.Title && Artist == other.Artist && App == other.App && Playing == other.Playing
        && ((Cover is null && other.Cover is null) || (Cover is not null && other.Cover is not null && Cover.AsSpan().SequenceEqual(other.Cover)));
    public override int GetHashCode() => HashCode.Combine(Title, Artist, App, Playing, Cover?.Length ?? 0);
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

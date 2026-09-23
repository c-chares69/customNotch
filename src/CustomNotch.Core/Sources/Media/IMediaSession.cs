namespace CustomNotch.Core.Sources.Media;

/// <summary>Ce qui joue en ce moment, tel que le système le rapporte.</summary>
public sealed record MediaState(string Title, string Artist, string App, bool Playing);

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

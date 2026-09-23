using CustomNotch.Core.Config;
using CustomNotch.Core.Model;
using CustomNotch.Core.Sources;

namespace CustomNotch.App;

/// <summary>Ce qu'une PillWindow demande au contrôleur : les vues à dessiner, les actions, la sauvegarde de sa position.
/// Une interface plutôt que le contrôleur lui-même, pour que les fenêtres se testent avec un hôte factice.</summary>
public interface IPillHost
{
    /// <summary>L'apparence partagée courante (activité, échelle de la carte) : PillWindow y lit CardScale à
    /// chaque reconstruction de ses métriques, sans passer par _config directement.</summary>
    AppearanceConfig Appearance { get; }
    CellView? View(string cellId);
    /// <summary>Les vues des enfants d'une cellule-groupe, dans l'ordre de la config.</summary>
    IReadOnlyList<CellView> Children(CellConfig group);
    Task RunActionAsync(string cellId, ActionConfig action);
    /// <summary>Une action déclarée par la source elle-même (Reading.Actions) : play, stop, open…</summary>
    Task InvokeSourceAsync(string cellId, string action);
    void SavePosition(string pillId, double along, string? screen);
    void RequestRefresh(string cellId);
    void HidePill(string pillId);
    void ShowSettings();
    SourceSchema? Schema(string sourceType);
}

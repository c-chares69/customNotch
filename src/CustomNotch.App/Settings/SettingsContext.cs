using CustomNotch.Core;
using CustomNotch.Core.Config;
using CustomNotch.Core.Sources;

namespace CustomNotch.App.Settings;

/// <summary>Ce que les pages de réglages reçoivent : l'état (Store), la plume (Editor), le catalogue (Registry),
/// et le vérificateur de mises à jour (Updates) que la carte « Mises à jour » de GeneralPage pilote.</summary>
public sealed record SettingsContext(ConfigStore Store, ConfigEditor Editor, SourceRegistry Registry, UpdateChecker Updates);

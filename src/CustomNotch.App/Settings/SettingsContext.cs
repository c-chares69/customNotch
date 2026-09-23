using CustomNotch.Core.Config;
using CustomNotch.Core.Sources;

namespace CustomNotch.App.Settings;

/// <summary>Ce que les pages de réglages reçoivent : l'état (Store), la plume (Editor), le catalogue (Registry).</summary>
public sealed record SettingsContext(ConfigStore Store, ConfigEditor Editor, SourceRegistry Registry);

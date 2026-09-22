namespace CustomNotch.Core;

/// <summary>Identité de l'application : nom du dossier de données, AppUserModelID, version (Directory.Build.props).</summary>
public static class App
{
    public const string Name = "customNotch";
    public const string Id = "DevPilot.CustomNotch";
    public const string Company = "DevPilot";
    public static readonly string Version = (typeof(App).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
        .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion ?? "0.0.0").Split('+')[0];
    public const string HomeEnv = "CUSTOMNOTCH_HOME";
    /// <summary>Accent par défaut de la fenêtre de réglages quand Windows n'en fournit pas.</summary>
    public const string DefaultAccent = "#3B82F6";
}

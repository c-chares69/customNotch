using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace CustomNotch.Core;

/// <summary>
/// Chiffrement des secrets avec le compte Windows (DPAPI) - le même format que la
/// version Python, pour que le fichier de configuration se lise d'une version à l'autre :
/// préfixe <c>dpapi:</c>, blob en base64, entropie secondaire liée à l'application.
/// </summary>
/// <remarks>
/// DPAPI protège au repos (autre compte, disque copié, sauvegarde cloud), pas contre un
/// programme qui s'exécute sous le compte de l'utilisateur. Hors Windows, tout passe en
/// clair : les fonctions deviennent l'identité.
/// </remarks>
public static class Secrets
{
    public const string Prefix = "dpapi:";
    private static readonly byte[] Entropy = Encoding.ASCII.GetBytes("customNotch.secret.v1");

    /// <summary>Chiffre une valeur pour le disque. Rend le clair inchangé hors Windows,
    /// ou si le chiffrement échoue : mieux vaut un token en clair qu'un token perdu.</summary>
    public static string Seal(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.StartsWith(Prefix, StringComparison.Ordinal)
            || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return value ?? "";
        }
        try
        {
            var blob = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.CurrentUser);
            return Prefix + Convert.ToBase64String(blob);
        }
        catch (CryptographicException)
        {
            return value;
        }
    }

    /// <summary>Déchiffre une valeur du disque. Le clair d'une version antérieure passe
    /// tel quel ; un blob illisible (autre compte, autre machine) rend une chaîne vide,
    /// et l'application demande le secret à nouveau au lieu d'envoyer un blob à l'API.</summary>
    public static string Unseal(string? value)
    {
        if (string.IsNullOrEmpty(value) || !value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return value ?? "";
        }
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "";
        }
        try
        {
            var blob = Convert.FromBase64String(value[Prefix.Length..]);
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(blob, Entropy, DataProtectionScope.CurrentUser));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return "";
        }
    }
}

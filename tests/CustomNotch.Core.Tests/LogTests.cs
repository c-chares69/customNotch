using Xunit;
namespace CustomNotch.Core.Tests;

public class LogTests
{
    [Fact]
    public void Un_secret_enregistre_est_masque()
    {
        Log.Mask(new[] { "s3cr3t" });
        var redacted = Log.Redact("open https://x/?t=s3cr3t");
        Assert.DoesNotContain("s3cr3t", redacted);
    }

    [Fact]
    public void Un_secret_trop_court_n_est_pas_enregistre()
    {
        // < 4 caractères : le masquer caviarderait n'importe quel texte qui le contient par hasard.
        Log.Mask(new[] { "ab" });
        var redacted = Log.Redact("le mot abricot reste lisible");
        Assert.Contains("abricot", redacted);
    }

    [Fact]
    public void Le_caviardage_par_motif_connu_fonctionne_toujours()
    {
        var redacted = Log.Redact("Authorization: Bearer abcdef1234567890");
        Assert.DoesNotContain("1234567890", redacted);
        Assert.Contains("Bearer", redacted);
    }
}

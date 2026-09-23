using CustomNotch.Core.Sources.Claude;
using Xunit;
namespace CustomNotch.Core.Tests.Sources.Claude;

public class BackoffTests
{
    [Theory]
    [InlineData(1, null, 60_000L)]
    [InlineData(3, null, 240_000L)]
    [InlineData(20, null, 3_600_000L)]
    [InlineData(1, 30_000L, 30_000L)]
    public void NextMs_suit_le_doublement_plafonne(int failures, long? retryAfterMs, long expected)
    {
        Assert.Equal(expected, Backoff.NextMs(failures, retryAfterMs));
    }

    [Fact]
    public void Save_puis_Load_rendent_la_meme_valeur()
    {
        var dir = Directory.CreateTempSubdirectory("customnotch-backoff-tests-");
        try
        {
            var path = Path.Combine(dir.FullName, "claude-backoff.json");

            Backoff.Save(path, 123_456);
            Assert.Equal(123_456, Backoff.Load(path));

            Backoff.Save(path, null);
            Assert.Null(Backoff.Load(path));
        }
        finally { dir.Delete(recursive: true); }
    }

    [Fact]
    public void Load_sans_fichier_rend_null()
    {
        Assert.Null(Backoff.Load(Path.Combine(Path.GetTempPath(), $"customnotch-{Guid.NewGuid():N}.json")));
    }
}

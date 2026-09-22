using CustomNotch.App.Cells;
using Xunit;
namespace CustomNotch.App.Tests;

public class GlyphLibraryTests
{
    [Theory]
    [InlineData("cpu")] [InlineData("memory")] [InlineData("disk")] [InlineData("network")] [InlineData("battery")]
    [InlineData("link")] [InlineData("globe")] [InlineData("terminal")] [InlineData("folder")] [InlineData("claude")]
    public void Les_glyphes_nommes_existent(string name) => Assert.False(GlyphLibrary.Get(name).Data.IsEmpty());

    [Fact]
    public void Un_trace_brut_est_accepte_et_plein()
    {
        var (data, filled) = GlyphLibrary.Get("M0,0 L10,5 L0,10 Z");
        Assert.True(filled);
        Assert.Equal(10, data.Bounds.Width, 0.1);
    }

    [Fact]
    public void Inconnu_ou_vide_donne_le_point()
    {
        Assert.Equal(GlyphLibrary.Get("dot").Data.ToString(), GlyphLibrary.Get("zzz").Data.ToString());
        Assert.Equal(GlyphLibrary.Get("dot").Data.ToString(), GlyphLibrary.Get(null).Data.ToString());
    }
}

namespace RiksrevisjonApi.Tests;

public class OrgNormalisationTests
{
    [Fact]
    public void NormalizeDepartment_StripsTrailingDot()
    {
        Assert.Equal("helse", MatchService.NormalizeDepartment("Helse ·"));
        Assert.Equal("helse", MatchService.NormalizeDepartment("Helse"));
    }

    [Fact]
    public void NormalizeDepartment_ReturnsFirstSegmentOnSlash()
    {
        Assert.Equal("forsvar", MatchService.NormalizeDepartment("Forsvar/sikkerhet/beredskap ·"));
        Assert.Equal("forsvar", MatchService.NormalizeDepartment("Forsvar/sikkerhet/beredskap"));
    }

    [Fact]
    public void NormalizeDepartment_LowercasesResult()
    {
        Assert.Equal("offentlig", MatchService.NormalizeDepartment("Offentlig forvaltning"));
        Assert.Equal("statens", MatchService.NormalizeDepartment("Statens eierstyring"));
    }

    [Fact]
    public void NormalizeDepartment_ReturnsEmptyForBlankInput()
    {
        Assert.Equal("", MatchService.NormalizeDepartment(""));
        Assert.Equal("", MatchService.NormalizeDepartment("   "));
        Assert.Equal("", MatchService.NormalizeDepartment(null));
    }

    [Fact]
    public void NormalizeDepartment_ReturnsEmptyForOnlyDotInput()
    {
        // "  ·  " → TrimEnd() → "  ·" → TrimEnd('·') → "  " → Trim() → "" → return ""
        Assert.Equal("", MatchService.NormalizeDepartment("  ·  "));
        Assert.Equal("", MatchService.NormalizeDepartment(" · "));
    }
}

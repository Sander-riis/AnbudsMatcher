namespace RiksrevisjonApi.Tests;

public class KeywordExtractionTests
{
    [Fact]
    public void ExtractKeywords_RemovesNorwegianStopwords()
    {
        var result = MatchService.ExtractKeywords("helfo og nav");
        Assert.Contains("helfo", result);
        Assert.Contains("nav", result);
        Assert.DoesNotContain("og", result);
    }

    [Fact]
    public void ExtractKeywords_FiltersShortWords()
    {
        var result = MatchService.ExtractKeywords("IT og IKT");
        Assert.DoesNotContain("it", result);   // length 2 — filtered
        Assert.Contains("ikt", result);         // length 3 — kept
    }

    [Fact]
    public void ExtractKeywords_KeepsMeaningfulNorwegianWords()
    {
        var result = MatchService.ExtractKeywords("kjøp av helsetjenester");
        Assert.Contains("helsetjenester", result);
        Assert.DoesNotContain("av", result);
        Assert.Contains("kjøp", result);
    }

    [Fact]
    public void ExtractKeywords_PreservesNorwegianCharacters()
    {
        var result = MatchService.ExtractKeywords("Riksrevisjonens spesialisthelsetjenester");
        Assert.Contains("spesialisthelsetjenester", result);
        Assert.DoesNotContain("riksrevisjonens", result);  // in Stopwords list
    }

    [Fact]
    public void ExtractKeywords_ReturnsDistinctWords()
    {
        var result = MatchService.ExtractKeywords("helfo helfo helfo");
        Assert.Equal(1, result.Count(w => w == "helfo"));
    }

    [Fact]
    public void ExtractKeywords_ReturnsEmptyForNullOrWhitespace()
    {
        Assert.Empty(MatchService.ExtractKeywords(""));
        Assert.Empty(MatchService.ExtractKeywords("   "));
        Assert.Empty(MatchService.ExtractKeywords(null));
    }
}

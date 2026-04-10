namespace RiksrevisjonApi.Tests;

public class EntityTermExtractionTests
{
    [Fact]
    public void ExtractEntityTerms_ExtractsLongWordsFromReportTitles()
    {
        var reports = new List<Report>
        {
            new(1, "Helseplattformen i Midt-Norge", "", "", "", "", ""),
            new(2, "Digitalisering av tolletaten", "", "", "", "", ""),
        };
        var terms = DoffinService.ExtractEntityTerms(reports);
        Assert.Contains("helseplattformen", terms);
        Assert.Contains("digitalisering", terms);
        Assert.DoesNotContain("midt", terms);     // too short
        Assert.DoesNotContain("norge", terms);     // too short
    }

    [Fact]
    public void ExtractEntityTerms_ExcludesStopwords()
    {
        var reports = new List<Report>
        {
            new(1, "Riksrevisjonens undersøkelse av Helseplattformen", "", "", "", "", ""),
        };
        var terms = DoffinService.ExtractEntityTerms(reports);
        Assert.DoesNotContain("riksrevisjonens", terms);
        Assert.DoesNotContain("undersøkelse", terms);
        Assert.Contains("helseplattformen", terms);
    }

    [Fact]
    public void ExtractEntityTerms_DeduplicatesAcrossReports()
    {
        var reports = new List<Report>
        {
            new(1, "Helseplattformen rapport", "", "", "", "", ""),
            new(2, "Helseplattformen oppfølging", "", "", "", "", ""),
        };
        var terms = DoffinService.ExtractEntityTerms(reports);
        Assert.Single(terms.Where(t => t == "helseplattformen"));
    }

    [Fact]
    public void ExtractEntityTerms_ReturnsEmptyForEmptyReports()
    {
        var terms = DoffinService.ExtractEntityTerms(new List<Report>());
        Assert.Empty(terms);
    }
}

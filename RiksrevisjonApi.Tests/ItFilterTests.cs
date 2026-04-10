using System.Text.Json;

namespace RiksrevisjonApi.Tests;

/// <summary>
/// IT-01: ParseListPage correctly parses /rapporter/?q=Digitalisering/ikt HTML.
/// IT-03: IsServiceContract returns true for "Kontraktens art" = "Tjenester" (new eForm format).
/// IT-04: IsServiceContract returns true for "Nature of the contract" = "Services" (old format).
/// </summary>
public class ItFilterTests
{
    // Minimal sample HTML matching the rr-link-wrapper structure verified in RESEARCH.md.
    // Uses the same regex targets as ParseListPage: rr-link-wrapper, blockquote, <time>, <h2>, rr-search-result-meta.
    private const string SampleListPageHtml = """
        <a class="rr-link-wrapper" href="/rapporter-mappe/2025/riksrevisjonens-kontroll-med-it-systemer/">
          <div class="rr-search-result-meta">Digitalisering/ikt</div>
          <h2 class="rr-heading">Riksrevisjonens kontroll med IT-systemer</h2>
          <blockquote><p>Undersøkelse av digitalisering i offentlig sektor.</p></blockquote>
          <time datetime="2025-03-15">15.03.2025</time>
        </a>
        """;

    [Fact]
    public void ParseListPage_ItCategory_ReturnsNonEmptyList()
    {
        // IT-01: The existing ParseListPage regex works unchanged against the IT-category URL HTML.
        var results = ReportService.ParseListPage(SampleListPageHtml, page: 1);

        Assert.NotEmpty(results);
        Assert.Equal(1, results.Count);
        Assert.Equal("Riksrevisjonens kontroll med IT-systemer", results[0].Title);
        Assert.Contains("riksrevisjonen.no/rapporter-mappe/", results[0].Url);
    }

    [Fact]
    public void IsServiceContract_NewFormat_ReturnsTrueForTjenester()
    {
        // IT-03: eform JSON with label="Kontraktens art", value="Tjenester" (2023+ Norwegian format)
        // Structure: array of blocks → sections (L1) → sections (L2) → sections (L3/leaf with label+value)
        var eformJson = """
            [
              {
                "sections": [
                  {
                    "sections": [
                      {
                        "sections": [
                          { "label": "Kontraktens art", "value": "Tjenester" }
                        ]
                      }
                    ]
                  }
                ]
              }
            ]
            """;
        var eform = JsonDocument.Parse(eformJson).RootElement;

        Assert.True(DoffinService.IsServiceContract(eform));
    }

    [Fact]
    public void IsServiceContract_OldFormat_ReturnsTrueForServices()
    {
        // IT-04: eform JSON with label="Nature of the contract", value="Services" (pre-2023 English format)
        var eformJson = """
            [
              {
                "sections": [
                  {
                    "sections": [
                      {
                        "sections": [
                          { "label": "Nature of the contract", "value": "Services" }
                        ]
                      }
                    ]
                  }
                ]
              }
            ]
            """;
        var eform = JsonDocument.Parse(eformJson).RootElement;

        Assert.True(DoffinService.IsServiceContract(eform));
    }
}

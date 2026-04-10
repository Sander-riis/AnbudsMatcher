using System.Text.Json;

namespace RiksrevisjonApi.Tests;

/// <summary>
/// IT-01: ParseListPage correctly parses /rapporter/?q=Digitalisering/ikt HTML.
/// IT-03: IsServiceContract returns true for "Kontraktens art" = "Tjenester" (new eForm format).
/// IT-04: IsServiceContract returns true for "Nature of the contract" = "Services" (old format).
/// </summary>
public class ItFilterTests
{
    // Minimal sample HTML matching the rr-search-result-link structure from /sok/?t=1&cat=10.
    private const string SampleSearchPageHtml = """
        <article>
          <header>
            <div class="rr-search-result-info">
              <span class="rr-search-result-info__tag rr-font--heavy">Rapport</span>
              <span class="rr-search-result-info__tag">
                Publisert
                <time datetime="15.03.2025 09:00:00">15.03.2025</time>
              </span>
            </div>
            <a class="rr-search-result-link" href="/rapporter-mappe/2025/riksrevisjonens-kontroll-med-it-systemer/">
              <h3 class="rr-heading-3 rr-font--neutral">Riksrevisjonens kontroll med IT-systemer</h3>
            </a>
          </header>
          <blockquote>
            <p class="rr-search-result__excerpt">Undersøkelse av digitalisering i offentlig sektor.</p>
          </blockquote>
        </article>
        """;

    [Fact]
    public void ParseListPage_ItCategory_ReturnsNonEmptyList()
    {
        // IT-01: ParseSearchPage correctly parses /sok/?t=1&cat=10 search results HTML.
        var results = ReportService.ParseSearchPage(SampleSearchPageHtml);

        Assert.NotEmpty(results);
        Assert.Single(results);
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

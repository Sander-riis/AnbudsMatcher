namespace RiksrevisjonApi.Tests;

/// <summary>
/// IT-01: ParseListPage correctly parses /rapporter/?q=Digitalisering/ikt HTML.
/// IT-03: IsServiceContract returns true for "Kontraktens art" = "Tjenester" (new eForm format).
/// IT-04: IsServiceContract returns true for "Nature of the contract" = "Services" (old format).
/// 
/// Wave 0 stubs — bodies replaced in Plan 05-01 when internal helpers are added to Program.cs.
/// </summary>
public class ItFilterTests
{
    [Fact]
    public void ParseListPage_ItCategory_ReturnsNonEmptyList()
    {
        // IT-01: ReportService.ParseListPage must become internal static in Plan 05-01.
        // Sample HTML uses the same rr-link-wrapper structure verified in RESEARCH.md.
        Assert.Fail("Wave 0 stub — pending Plan 05-01 (ParseListPage made internal)");
    }

    [Fact]
    public void IsServiceContract_NewFormat_ReturnsTrueForTjenester()
    {
        // IT-03: DoffinService.IsServiceContract(eform) where label="Kontraktens art", value="Tjenester"
        Assert.Fail("Wave 0 stub — pending Plan 05-01 (IsServiceContract added to DoffinService)");
    }

    [Fact]
    public void IsServiceContract_OldFormat_ReturnsTrueForServices()
    {
        // IT-04: DoffinService.IsServiceContract(eform) where label="Nature of the contract", value="Services"
        Assert.Fail("Wave 0 stub — pending Plan 05-01 (IsServiceContract added to DoffinService)");
    }
}

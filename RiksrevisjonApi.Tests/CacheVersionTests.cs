namespace RiksrevisjonApi.Tests;

/// <summary>
/// IT-05: When the notices cache file contains a version string that does not match
/// the current SearchVersion, LoadAsync must delete all three cache files before scraping.
///
/// Wave 0 stub — body replaced in Plan 05-02 when CheckVersionMismatch + PurgeAllCaches
/// are added as internal static helpers to DoffinService.
/// </summary>
public class CacheVersionTests
{
    [Fact]
    public void VersionMismatch_DeletesAllThreeCacheFiles()
    {
        // IT-05: Plant a notices-cache.json with version="old-version", also plant
        // reports-cache.json and matches-cache.json. Then call DoffinService.CheckVersionMismatch
        // and DoffinService.PurgeAllCaches. Verify all three files are gone.
        // DoffinService.CheckVersionMismatch and PurgeAllCaches must become internal static in Plan 05-02.
        Assert.Fail("Wave 0 stub — pending Plan 05-02 (CheckVersionMismatch + PurgeAllCaches added to DoffinService)");
    }
}

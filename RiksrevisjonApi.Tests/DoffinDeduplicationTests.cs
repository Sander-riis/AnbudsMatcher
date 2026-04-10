namespace RiksrevisjonApi.Tests;

/// <summary>
/// IT-02: Multi-term Doffin scrape deduplicates by URL, keeps first occurrence.
/// IT-06: After deduplication, IDs are re-assigned sequentially starting at 1.
///
/// Wave 0 stubs — bodies replaced in Plan 05-01 when DeduplicateNotices is added to DoffinService.
/// </summary>
public class DoffinDeduplicationTests
{
    [Fact]
    public void DeduplicateNotices_RemovesDuplicateUrls_KeepsFirst()
    {
        // IT-02: Given two Notice lists with one overlapping URL, merged result has no URL duplicates.
        // DoffinService.DeduplicateNotices must become internal static in Plan 05-01.
        Assert.Fail("Wave 0 stub — pending Plan 05-01 (DeduplicateNotices added to DoffinService)");
    }

    [Fact]
    public void DeduplicateNotices_ReassignsIds_SequentialFromOne()
    {
        // IT-06: After deduplication, notice IDs are 1, 2, 3... regardless of original IDs.
        Assert.Fail("Wave 0 stub — pending Plan 05-01 (DeduplicateNotices added to DoffinService)");
    }
}

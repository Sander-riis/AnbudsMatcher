namespace RiksrevisjonApi.Tests;

/// <summary>
/// IT-02: Multi-term Doffin scrape deduplicates by URL, keeps first occurrence.
/// IT-06: After deduplication, IDs are re-assigned sequentially starting at 1.
/// </summary>
public class DoffinDeduplicationTests
{
    [Fact]
    public void DeduplicateNotices_RemovesDuplicateUrls_KeepsFirst()
    {
        // IT-02: Two terms both return a notice for the same URL.
        // After dedup the URL appears exactly once, and the first occurrence (Id=1) is kept.
        var termOneNotices = new List<Notice>
        {
            new(1, "IKT-anskaffelse", "Direktoratet", "2025-01-10", "https://www.doffin.no/notices/2025-001", "desc A"),
            new(2, "Programvareutvikling", "Oslo kommune", "2025-02-01", "https://www.doffin.no/notices/2025-002", "desc B"),
        };
        var termTwoNotices = new List<Notice>
        {
            // Duplicate of the first notice (same URL, different temp ID from second term scrape)
            new(3, "IKT-anskaffelse", "Direktoratet", "2025-01-10", "https://www.doffin.no/notices/2025-001", "desc A"),
            new(4, "Skyløsning", "NAV", "2025-03-15", "https://www.doffin.no/notices/2025-003", "desc C"),
        };

        var merged = termOneNotices.Concat(termTwoNotices).ToList();
        var deduped = DoffinService.DeduplicateNotices(merged);

        Assert.Equal(3, deduped.Count);  // 4 total − 1 duplicate = 3 unique
        Assert.Single(deduped, n => n.Url == "https://www.doffin.no/notices/2025-001");
    }

    [Fact]
    public void DeduplicateNotices_ReassignsIds_SequentialFromOne()
    {
        // IT-06: After dedup, IDs must be 1, 2, 3... regardless of original IDs.
        var notices = new List<Notice>
        {
            new(42, "Notice A", "Buyer A", "2025-01-01", "https://www.doffin.no/notices/A", ""),
            new(99, "Notice B", "Buyer B", "2025-01-02", "https://www.doffin.no/notices/B", ""),
            new(7,  "Notice C", "Buyer C", "2025-01-03", "https://www.doffin.no/notices/C", ""),
        };

        var deduped = DoffinService.DeduplicateNotices(notices);

        Assert.Equal(3, deduped.Count);
        Assert.Equal(1, deduped[0].Id);
        Assert.Equal(2, deduped[1].Id);
        Assert.Equal(3, deduped[2].Id);
    }
}

namespace RiksrevisjonApi.Tests;

public class IntegrationTests
{
    // Hard-coded test data — no real services, no HTTP, no Playwright needed
    private static readonly List<Report> TestReports =
    [
        new(1, "Helfo helsetjenester spesialistbehandling",
            "kjøp av spesialisthelsetjenester for pasienter",
            "Kritikkverdig", "2024-01-01", "Helse ·", "https://riksrevisjonen.no/test1"),
        new(2, "Forsvarets anskaffelse av utstyr",
            "forsvarsmateriell og sikkerhetsutstyr",
            "Ingen karakter", "2024-01-02", "Forsvar/sikkerhet/beredskap ·",
            "https://riksrevisjonen.no/test2"),
        new(3, "x", "y",  // title+summary too short → no keywords extracted → skipped
            "Ingen karakter", "2024-01-03", "Offentlig forvaltning",
            "https://riksrevisjonen.no/test3"),
    ];

    private static readonly List<Notice> TestNotices =
    [
        new(42, "Kjøp av spesialisthelsetjenester",
            "Norsk Helsenett SF", "2024-01-01", "https://doffin.no/42",
            "Anskaffelse helsetjenester pasientbehandling"),
        new(99, "Forsvarsmateriell kontrakt",
            "Forsvarsbygg", "2024-01-01", "https://doffin.no/99",
            "Anskaffelse forsvarutstyr"),
    ];

    [Fact]
    public void ComputeMatches_ReturnsOnlyScoresAboveFifteen()
    {
        var matches = MatchService.ComputeMatches(TestReports, TestNotices);
        Assert.All(matches, m => Assert.True(m.Score > 15,
            $"Match score {m.Score} is not > 15 (reportId={m.ReportId}, noticeId={m.NoticeId})"));
    }

    [Fact]
    public void ComputeMatches_EachMatchHasRequiredFields()
    {
        var matches = MatchService.ComputeMatches(TestReports, TestNotices);
        Assert.NotEmpty(matches);
        Assert.All(matches, m =>
        {
            Assert.True(m.ReportId > 0, "ReportId must be > 0");
            Assert.True(m.NoticeId > 0, "NoticeId must be > 0");
            Assert.True(m.Score > 0,    "Score must be > 0");
            Assert.NotNull(m.MatchedKeywords);
        });
    }

    [Fact]
    public void ComputeMatches_HealthReportMatchesHealthSectorNotice()
    {
        // Report 1 (dept="Helse ·" → normDept="helse") vs Notice 42 (buyer="Norsk Helsenett SF")
        // "norsk helsenett sf".Contains("helse") = true → orgScore = 100 → combined >= 40 > 15
        var matches = MatchService.ComputeMatches(TestReports, TestNotices);
        Assert.Contains(matches, m => m.ReportId == 1 && m.NoticeId == 42);
    }
}

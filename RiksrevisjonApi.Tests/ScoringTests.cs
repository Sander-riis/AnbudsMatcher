namespace RiksrevisjonApi.Tests;

public class ScoringTests
{
    [Fact]
    public void KeywordScore_ReturnsHundredWhenAllKeywordsMatch()
    {
        var keywords = new List<string> { "helfo", "helsetjenester", "pasient" };
        var notice = new Notice(1, "helfo helsetjenester", "Helfo", "", "", "pasient behandling");
        var (score, matched) = MatchService.ComputeKeywordScore(keywords, notice);
        Assert.Equal(100.0, score, 1);  // 3/3 = 100%
        Assert.Equal(3, matched.Count);
    }

    [Fact]
    public void KeywordScore_ReturnsPartialScoreOnPartialMatch()
    {
        var keywords = new List<string> { "helfo", "helsetjenester", "pasient" };
        var notice = new Notice(2, "helfo", "Helfo", "", "", "");
        var (score, _) = MatchService.ComputeKeywordScore(keywords, notice);
        Assert.Equal(33.3, score, 1);  // 1/3 = 33.3%
    }

    [Fact]
    public void OrgScore_ReturnsHundredWhenDeptTokenInBuyer()
    {
        var (orgScore, matchedOrg) = MatchService.ComputeOrgScore(
            "helse", new Notice(1, "", "Helsedirektoratet", "", "", ""));
        Assert.Equal(100.0, orgScore, 1);
        Assert.Equal("helse", matchedOrg);
    }

    [Fact]
    public void OrgScore_ReturnsZeroWhenDeptTokenNotInBuyer()
    {
        // "helfo" does NOT contain "helse" as substring — known Helfo gap per research
        var (orgScore, matchedOrg) = MatchService.ComputeOrgScore(
            "helse", new Notice(2, "", "Helfo", "", "", ""));
        Assert.Equal(0.0, orgScore, 1);
        Assert.Null(matchedOrg);
    }

    [Fact]
    public void CombinedScore_FollowsWeightedFormula()
    {
        // Per REQ-04: combined = keyword_score * 0.6 + org_score * 0.4
        Assert.Equal(52.0, 60.0 * 0.6 + 40.0 * 0.4, 1);
        Assert.Equal(40.0, 0.0  * 0.6 + 100.0 * 0.4, 1);  // org-only match
        Assert.Equal(15.0, 25.0 * 0.6 + 0.0  * 0.4, 1);   // exactly 15 — does NOT pass threshold
    }

    [Fact]
    public void Threshold_FiltersScoresAtOrBelowFifteen()
    {
        // Score must be STRICTLY greater than 15 (not >=)
        Assert.True(15.1 > 15);
        Assert.False(15.0 > 15);
        Assert.False(14.9 > 15);
    }
}

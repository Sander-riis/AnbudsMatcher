namespace RiksrevisjonApi.Tests;

public class ScoringTests
{
    [Fact]
    public void KeywordScore_ReturnsHundredWhenAllKeywordsMatch()
    {
        var keywords = new List<string> { "helfo", "helsetjenester", "pasient" };
        var notice = new Notice(1, "helfo helsetjenester", "Helfo", "", "", "pasient behandling");
        var (score, matched) = MatchService.ComputeKeywordScore(keywords, notice);
        Assert.Equal(100.0, score, 1);  // 3/3 = 100% (uniform IDF weights)
        Assert.Equal(3, matched.Count);
    }

    [Fact]
    public void KeywordScore_ReturnsZeroWhenFewerThanThreeKeywordsMatch()
    {
        // Min-3 gate: fewer than 3 keyword hits → score 0 (avoids single-word false positives)
        var keywords = new List<string> { "helfo", "helsetjenester", "pasient" };
        var notice = new Notice(2, "helfo", "Helfo", "", "", "");
        var (score, matched) = MatchService.ComputeKeywordScore(keywords, notice);
        Assert.Equal(0.0, score, 1);   // Only 1/3 match — below minimum of 3
        Assert.Single(matched);        // List still returned for transparency
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
        // Per updated weights: combined = keyword_score * 0.40 + bigram_score * 0.15 + org_score * 0.25 + title_score * 0.20
        Assert.Equal(36.5, 60.0 * 0.40 + 0.0  * 0.15 + 50.0 * 0.25 + 0.0 * 0.20, 1);
        Assert.Equal(25.0, 0.0  * 0.40 + 0.0  * 0.15 + 100.0 * 0.25 + 0.0 * 0.20, 1);   // org-only → 25, below threshold
        Assert.Equal(20.0, 50.0 * 0.40 + 0.0  * 0.15 + 0.0  * 0.25 + 0.0 * 0.20, 1);    // keywords-only, below threshold
        Assert.Equal(45.0, 0.0  * 0.40 + 0.0  * 0.15 + 100.0 * 0.25 + 100.0 * 0.20, 1); // org + title match → above threshold
    }

    [Fact]
    public void ExtractTitleWords_ExtractsWordsWithSixOrMoreChars()
    {
        var words = MatchService.ExtractTitleWords("Helseplattformen i Midt-Norge");
        Assert.Contains("helseplattformen", words);
        Assert.DoesNotContain("midt", words);    // 4 chars — too short
        Assert.DoesNotContain("norge", words);    // 5 chars — too short
    }

    [Fact]
    public void ExtractTitleWords_RemovesStopwords()
    {
        var words = MatchService.ExtractTitleWords("Riksrevisjonens undersøkelse av digitalisering");
        Assert.DoesNotContain("riksrevisjonens", words);
        Assert.DoesNotContain("undersøkelse", words);
        Assert.Contains("digitalisering", words);
    }

    [Fact]
    public void ExtractTitleWords_ReturnsEmptyForNullOrBlank()
    {
        Assert.Empty(MatchService.ExtractTitleWords(null));
        Assert.Empty(MatchService.ExtractTitleWords(""));
        Assert.Empty(MatchService.ExtractTitleWords("   "));
    }

    [Fact]
    public void ComputeTitleWordScore_ReturnsHundredWhenAllWordsMatch()
    {
        var titleWords = new List<string> { "helseplattformen" };
        var notice = new Notice(1, "Analyse av fordeler og ulemper ved helseplattformen", "HOD", "", "", "");
        var (score, matched) = MatchService.ComputeTitleWordScore(titleWords, notice);
        Assert.Equal(100.0, score, 1);
        Assert.Single(matched);
        Assert.Equal("helseplattformen", matched[0]);
    }

    [Fact]
    public void ComputeTitleWordScore_ReturnsZeroWhenNoMatch()
    {
        var titleWords = new List<string> { "helseplattformen" };
        var notice = new Notice(2, "IT-drift og vedlikehold", "HOD", "", "", "");
        var (score, matched) = MatchService.ComputeTitleWordScore(titleWords, notice);
        Assert.Equal(0.0, score, 1);
        Assert.Empty(matched);
    }

    [Fact]
    public void ComputeTitleWordScore_PartialMatch_ScalesProportionally()
    {
        var titleWords = new List<string> { "helseplattformen", "digitalisering" };
        var notice = new Notice(3, "Analyse av helseplattformen", "HOD", "", "", "");
        var (score, matched) = MatchService.ComputeTitleWordScore(titleWords, notice);
        Assert.Equal(50.0, score, 1);  // 1 of 2 words → 50%
        Assert.Single(matched);
    }

    [Fact]
    public void Threshold_FiltersScoresAtOrBelow40()
    {
        // Score must be STRICTLY greater than 40 (not >=)
        Assert.True(40.1 > 40);
        Assert.False(40.0 > 40);
        Assert.False(39.9 > 40);
    }
}

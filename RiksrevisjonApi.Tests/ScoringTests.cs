namespace RiksrevisjonApi.Tests;

public class ScoringTests
{
    [Fact]
    public void KeywordScore_ReturnsHundredWhenAllKeywordsMatch()
        => throw new NotImplementedException("Wave 3 (02-03): implement MatchService.ComputeKeywordScore");

    [Fact]
    public void KeywordScore_ReturnsPartialScoreOnPartialMatch()
        => throw new NotImplementedException("Wave 3 (02-03): implement MatchService.ComputeKeywordScore");

    [Fact]
    public void OrgScore_ReturnsHundredWhenDeptTokenInBuyer()
        => throw new NotImplementedException("Wave 3 (02-03): implement MatchService.ComputeOrgScore");

    [Fact]
    public void OrgScore_ReturnsZeroWhenDeptTokenNotInBuyer()
        => throw new NotImplementedException("Wave 3 (02-03): implement MatchService.ComputeOrgScore");

    [Fact]
    public void CombinedScore_FollowsWeightedFormula()
        => throw new NotImplementedException("Wave 3 (02-03): combined = kw*0.6 + org*0.4");

    [Fact]
    public void Threshold_FiltersScoresAtOrBelowFifteen()
        => throw new NotImplementedException("Wave 3 (02-03): score > 15 (strictly greater)");
}

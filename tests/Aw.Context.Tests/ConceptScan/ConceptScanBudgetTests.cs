using Aw.Context.Core.ConceptScan;

namespace Aw.Context.Tests.ConceptScan;

public sealed class ConceptScanBudgetTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData(0)]
    [InlineData(5)]
    public void A_scan_may_start_below_the_daily_limit(int startedToday) => Assert.Null(ConceptScanBudget.Refusal(startedToday));

    [Theory, Trait("Lane", "unit")]
    [InlineData(6)]
    [InlineData(9)]
    public void The_seventh_scan_of_a_day_is_refused_with_a_reason_that_names_the_count_and_the_limit(int startedToday)
    {
        var reason = ConceptScanBudget.Refusal(startedToday);

        Assert.NotNull(reason);
        Assert.StartsWith("over budget:", reason, StringComparison.Ordinal);
        Assert.Contains(startedToday.ToString(System.Globalization.CultureInfo.InvariantCulture), reason, StringComparison.Ordinal);
        Assert.Contains("limit is 6", reason, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_limits_are_the_owners()
    {
        Assert.Equal(60_000, ConceptScanLimits.MaxInputChars);
        Assert.Equal(4_000, ConceptScanLimits.MaxOutputTokens);
        Assert.Equal(6, ConceptScanLimits.MaxScansPerDay);
        Assert.Equal(TimeSpan.FromMinutes(2), ConceptScanLimits.CallTimeout);
        Assert.Equal(TimeSpan.FromHours(24), ConceptScanLimits.Window);
    }
}

using Aw.Connectors.Core.Slack;

namespace Aw.Connectors.Tests.Slack;

public sealed class SlackTsTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_ts_round_trips_to_the_microsecond()
    {
        Assert.True(SlackTs.TryParse("1790000000.000123", out var at));

        Assert.Equal(new DateTimeOffset(2026, 9, 21, 14, 13, 20, TimeSpan.Zero).AddTicks(1230), at);
        Assert.Equal("1790000000.000123", SlackTs.Format(at));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("1790000000")]
    [InlineData("1790000000.1")]
    [InlineData("1790000000.00012a")]
    [InlineData("179000.0000.000123")]
    [InlineData("-1790000000.000123")]
    public void Text_that_is_not_a_ts_does_not_parse(string text) => Assert.False(SlackTs.TryParse(text, out _));

    [Fact, Trait("Lane", "unit")]
    public void The_day_start_is_midnight_utc_of_the_same_day()
    {
        Assert.Equal("1789948800.000000", SlackTs.DayStart("1790000000.000123"));
        Assert.Equal("2026-09-21", SlackTs.Day(DateTimeOffset.FromUnixTimeSeconds(1790000000)));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_permalink_is_the_workspace_archive_url_with_the_dot_dropped()
        => Assert.Equal("https://example.slack.com/archives/C0001GEN/p1790000000000123", SlackPermalink.For("example", "C0001GEN", "1790000000.000123"));
}

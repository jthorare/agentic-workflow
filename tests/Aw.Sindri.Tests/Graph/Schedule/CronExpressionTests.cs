using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Schedule;

namespace Aw.Sindri.Tests.Graph.Schedule;

/// <summary>A five-field cron with a timezone: the next tick, the latest missed tick, DST in America/Los_Angeles, and errors that name the field.</summary>
[Lane(TestLane.Unit)]
public sealed class CronExpressionTests
{
    private const string LosAngeles = "America/Los_Angeles";

    [Theory]
    [InlineData("* * * * *", null, "2026-11-07T12:00:30Z", "2026-11-07T12:01:00Z")]
    [InlineData("* * * * *", null, "2026-11-07T12:00:00Z", "2026-11-07T12:01:00Z")]
    [InlineData("*/15 * * * *", null, "2026-11-07T12:00:00Z", "2026-11-07T12:15:00Z")]
    [InlineData("*/15 * * * *", null, "2026-11-07T12:45:00Z", "2026-11-07T13:00:00Z")]
    [InlineData("0,30 * * * *", null, "2026-11-07T12:05:00Z", "2026-11-07T12:30:00Z")]
    [InlineData("1-5 * * * *", null, "2026-11-07T12:05:00Z", "2026-11-07T13:01:00Z")]
    [InlineData("10-20/5 * * * *", null, "2026-11-07T12:00:00Z", "2026-11-07T12:10:00Z")]
    [InlineData("10-20/5 * * * *", null, "2026-11-07T12:15:00Z", "2026-11-07T12:20:00Z")]
    [InlineData("0 9 * * *", null, "2026-11-07T09:00:00Z", "2026-11-08T09:00:00Z")]
    [InlineData("0 9,17 * * *", null, "2026-11-07T10:00:00Z", "2026-11-07T17:00:00Z")]
    [InlineData("0 0 1 * *", null, "2026-11-07T00:00:00Z", "2026-12-01T00:00:00Z")]
    [InlineData("0 0 1 1 *", null, "2026-11-07T00:00:00Z", "2027-01-01T00:00:00Z")]
    [InlineData("0 0 29 2 *", null, "2026-11-07T00:00:00Z", "2028-02-29T00:00:00Z")]
    [InlineData("0 0 * * 0", null, "2026-11-07T00:00:00Z", "2026-11-08T00:00:00Z")]
    [InlineData("0 0 * * 7", null, "2026-11-07T00:00:00Z", "2026-11-08T00:00:00Z")]
    [InlineData("0 0 * * 1-5", null, "2026-11-07T00:00:00Z", "2026-11-09T00:00:00Z")]
    [InlineData("0 0 * * MON", null, "2026-11-07T00:00:00Z", "2026-11-09T00:00:00Z")]
    [InlineData("0 0 1 JAN *", null, "2026-11-07T00:00:00Z", "2027-01-01T00:00:00Z")]
    public void Next_is_the_first_tick_strictly_after_the_instant_for_every_field_form(string cron, string? timezone, string after, string expected)
    {
        var schedule = Parse(cron, timezone);

        Assert.Equal(At(expected), schedule.Next(At(after)));
    }

    [Theory]
    [InlineData("0 0 1 * 1", "2026-11-07T00:00:00Z", "2026-11-09T00:00:00Z")]
    [InlineData("0 0 1 * 1", "2026-11-30T00:00:00Z", "2026-12-01T00:00:00Z")]
    [InlineData("0 0 15 * 5", "2026-11-07T00:00:00Z", "2026-11-13T00:00:00Z")]
    public void A_day_of_month_and_a_day_of_week_that_are_both_restricted_match_on_either(string cron, string after, string expected)
    {
        var schedule = Parse(cron, timezone: null);

        Assert.Equal(At(expected), schedule.Next(At(after)));
    }

    [Theory]
    [InlineData("0 9 * * *", "America/New_York", "2026-11-07T12:00:00Z", "2026-11-07T14:00:00Z")]
    [InlineData("30 8 * * *", "Asia/Tokyo", "2026-11-07T00:00:00Z", "2026-11-07T23:30:00Z")]
    [InlineData("0 9 * * *", LosAngeles, "2026-03-07T18:00:00Z", "2026-03-08T16:00:00Z")]
    public void Next_reads_the_fields_in_the_timezone(string cron, string timezone, string after, string expected)
    {
        var schedule = Parse(cron, timezone);

        Assert.Equal(At(expected), schedule.Next(At(after)));
    }

    [Theory]
    [InlineData("2026-03-08T00:00:00Z", "2026-03-08T10:00:00Z")]
    [InlineData("2026-03-08T10:00:00Z", "2026-03-09T09:30:00Z")]
    public void A_local_time_that_spring_forward_skips_fires_once_at_the_first_valid_instant_after_it(string after, string expected)
    {
        var schedule = Parse("30 2 * * *", LosAngeles);

        Assert.Equal(At(expected), schedule.Next(At(after)));
    }

    [Theory]
    [InlineData("2026-11-01T00:00:00Z", "2026-11-01T08:30:00Z")]
    [InlineData("2026-11-01T08:30:00Z", "2026-11-02T09:30:00Z")]
    public void A_local_time_that_fall_back_repeats_fires_once_at_its_first_occurrence(string after, string expected)
    {
        var schedule = Parse("30 1 * * *", LosAngeles);

        Assert.Equal(At(expected), schedule.Next(At(after)));
    }

    [Theory]
    [InlineData("*/15 * * * *", null, "2026-11-07T12:00:00Z", "2026-11-07T12:50:00Z", "2026-11-07T12:45:00Z")]
    [InlineData("*/15 * * * *", null, "2026-11-07T12:00:00Z", "2026-11-07T12:45:00Z", "2026-11-07T12:45:00Z")]
    [InlineData("0 9 * * *", null, "2026-11-05T10:00:00Z", "2026-11-07T20:00:00Z", "2026-11-07T09:00:00Z")]
    [InlineData("30 2 * * *", LosAngeles, "2026-03-07T00:00:00Z", "2026-03-08T12:00:00Z", "2026-03-08T10:00:00Z")]
    [InlineData("30 2 * * *", LosAngeles, "2026-03-07T12:00:00Z", "2026-03-08T12:00:00Z", "2026-03-08T10:00:00Z")]
    [InlineData("30 1 * * *", LosAngeles, "2026-10-31T12:00:00Z", "2026-11-01T12:00:00Z", "2026-11-01T08:30:00Z")]
    [InlineData("30 1 * * *", LosAngeles, "2026-10-31T12:00:00Z", "2026-11-01T08:30:00Z", "2026-11-01T08:30:00Z")]
    public void LastTick_returns_the_latest_missed_tick_across_a_DST_change_in_the_timezone(string cron, string? timezone, string after, string upTo, string expected)
    {
        var schedule = Parse(cron, timezone);

        Assert.Equal(At(expected), schedule.LastTick(At(after), At(upTo)));
    }

    [Theory]
    [InlineData("0 9 * * *", null, "2026-11-07T10:00:00Z", "2026-11-07T20:00:00Z")]
    [InlineData("*/15 * * * *", null, "2026-11-07T12:45:00Z", "2026-11-07T12:50:00Z")]
    [InlineData("30 2 * * *", LosAngeles, "2026-03-07T12:00:00Z", "2026-03-08T09:59:00Z")]
    [InlineData("30 1 * * *", LosAngeles, "2026-11-01T08:30:00Z", "2026-11-01T09:45:00Z")]
    [InlineData("* * * * *", null, "2026-11-07T12:00:00Z", "2026-11-07T12:00:00Z")]
    public void LastTick_is_null_when_no_tick_fell_after_the_first_instant_up_to_the_second(string cron, string? timezone, string after, string upTo)
    {
        var schedule = Parse(cron, timezone);

        Assert.Null(schedule.LastTick(At(after), At(upTo)));
    }

    [Theory]
    [InlineData("* * *", null, "5 fields", "minute hour day-of-month month day-of-week")]
    [InlineData("* * * * * *", null, "5 fields", "minute hour day-of-month month day-of-week")]
    [InlineData("", null, "5 fields", "minute hour day-of-month month day-of-week")]
    [InlineData("60 * * * *", null, "minute", "0-59")]
    [InlineData("abc * * * *", null, "minute", "0-59")]
    [InlineData("* 24 * * *", null, "hour", "0-23")]
    [InlineData("* * 0 * *", null, "day-of-month", "1-31")]
    [InlineData("* * 32 * *", null, "day-of-month", "1-31")]
    [InlineData("* * * 13 *", null, "month", "1-12")]
    [InlineData("* * * FOO *", null, "month", "1-12")]
    [InlineData("* * * * 8", null, "day-of-week", "0-7")]
    [InlineData("*/0 * * * *", null, "minute", "step")]
    [InlineData("5-1 * * * *", null, "minute", "range")]
    [InlineData("1,,2 * * * *", null, "minute", "empty")]
    public void A_bad_field_is_refused_naming_the_field_and_the_fix(string cron, string? timezone, string field, string fix)
    {
        Assert.False(CronExpression.TryParse(cron, timezone, out var expression, out var error));

        Assert.Null(expression);
        Assert.Contains(field, error, StringComparison.Ordinal);
        Assert.Contains(fix, error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Mars/Base")]
    [InlineData("")]
    [InlineData("PST8PDT-ish")]
    public void An_unknown_timezone_is_refused_naming_the_timezone_and_the_fix(string timezone)
    {
        Assert.False(CronExpression.TryParse("0 9 * * *", timezone, out var expression, out var error));

        Assert.Null(expression);
        Assert.Contains("timezone", error, StringComparison.Ordinal);
        Assert.Contains("IANA", error, StringComparison.Ordinal);
    }

    private static DateTimeOffset At(string instant) => DateTimeOffset.Parse(instant, CultureInfo.InvariantCulture);

    private static CronExpression Parse(string cron, string? timezone)
    {
        Assert.True(CronExpression.TryParse(cron, timezone, out var expression, out var error), error);
        return expression;
    }
}

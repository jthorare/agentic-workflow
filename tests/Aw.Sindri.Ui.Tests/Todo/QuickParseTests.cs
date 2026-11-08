using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Todo;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>The quick-add box's due-word rule (todo spec §3.2, D7), with today fixed to Sunday 2026-11-08.</summary>
[Lane(TestLane.Unit)]
public sealed class QuickParseTests
{
    private static readonly DateOnly _today = new(2026, 11, 8);

    private static (string Text, string? Due) Read(string typed, DateOnly? today = null)
    {
        var read = TodoQuickParse.Parse(typed, today ?? _today);
        return (read.Text, read.Due?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void A_trailing_weekday_becomes_the_next_one_and_leaves_the_text() =>
        Assert.Equal(("reply to Sam about the offsite", "2026-11-13"), Read("reply to Sam about the offsite fri"));

    [Theory]
    [InlineData("mon", "2026-11-09")]
    [InlineData("tue", "2026-11-10")]
    [InlineData("tues", "2026-11-10")]
    [InlineData("wed", "2026-11-11")]
    [InlineData("thu", "2026-11-12")]
    [InlineData("thurs", "2026-11-12")]
    [InlineData("fri", "2026-11-13")]
    [InlineData("sat", "2026-11-14")]
    [InlineData("Monday", "2026-11-09")]
    [InlineData("FRIDAY", "2026-11-13")]
    [InlineData("Sunday", "2026-11-15")]
    public void Weekday_names_and_short_forms_in_any_case_mean_the_next_one(string word, string due) =>
        Assert.Equal(("call", due), Read("call " + word));

    [Fact]
    public void A_weekday_that_is_today_means_next_week() =>
        Assert.Equal(("call", "2026-11-15"), Read("call sun"));

    [Fact]
    public void Today_and_tomorrow_are_read()
    {
        Assert.Equal(("pay rent", "2026-11-08"), Read("pay rent today"));
        Assert.Equal(("pay rent", "2026-11-09"), Read("pay rent tomorrow"));
    }

    [Fact]
    public void Tomorrow_rolls_over_the_month_and_the_year() =>
        Assert.Equal(("x", "2027-01-01"), Read("x tomorrow", new DateOnly(2026, 12, 31)));

    [Theory]
    [InlineData("11-10", "2026-11-10")]
    [InlineData("11-08", "2026-11-08")]
    [InlineData("12-31", "2026-12-31")]
    [InlineData("11-07", "2027-11-07")]
    [InlineData("01-02", "2027-01-02")]
    [InlineData("02-29", "2028-02-29")]
    public void A_month_and_day_is_the_next_such_date_so_a_passed_one_is_next_year(string word, string due) =>
        Assert.Equal(("file taxes", due), Read("file taxes " + word));

    [Fact]
    public void A_year_end_day_typed_on_the_last_day_of_the_year_is_that_day_and_the_new_year_follows() =>
        Assert.Equal([("a", "2026-12-31"), ("a", "2027-01-02")], [Read("a 12-31", new DateOnly(2026, 12, 31)), Read("a 01-02", new DateOnly(2026, 12, 31))]);

    [Theory]
    [InlineData("02-30")]
    [InlineData("13-01")]
    [InlineData("00-10")]
    [InlineData("11-00")]
    [InlineData("11-100")]
    [InlineData("11-")]
    [InlineData("1-2-3")]
    [InlineData("1-5")] // why: the box reads day words through DueWords, as the CLI does (spec §4.4), and DueWords wants two digits each side
    public void A_date_that_does_not_exist_stays_text(string word) =>
        Assert.Equal(("call " + word, null), Read("call " + word));

    [Fact]
    public void A_full_date_is_read_as_typed() =>
        Assert.Equal(("rotate the staging key", "2026-11-10"), Read("rotate the staging key 2026-11-10"));

    [Fact]
    public void A_closing_quote_keeps_the_day_word_as_text_and_drops_the_quote() =>
        Assert.Equal(("call mon", null), Read("call mon\""));

    [Theory]
    [InlineData("call tomorrow\"", "call tomorrow")]
    [InlineData("go 11-10\"", "go 11-10")]
    [InlineData("go today\"  ", "go today")]
    public void The_quote_works_for_every_kind_of_day_word(string typed, string saved) =>
        Assert.Equal((saved, null), Read(typed));

    [Fact]
    public void A_quote_after_a_word_that_is_not_a_day_stays_in_the_text() =>
        Assert.Equal(("say \"hi\"", null), Read("say \"hi\""));

    [Theory]
    [InlineData("fri")]
    [InlineData("tomorrow")]
    [InlineData("11-10")]
    public void A_day_word_with_nothing_before_it_is_the_text(string typed) =>
        Assert.Equal((typed, null), Read(typed));

    [Fact]
    public void Only_the_last_word_counts() =>
        Assert.Equal(("fri call mom", null), Read("fri call mom"));

    [Theory]
    [InlineData("buy friday2")]
    [InlineData("call monday-ish")]
    [InlineData("fix tomorrows bug")]
    public void A_word_that_only_starts_like_a_day_is_text(string typed) =>
        Assert.Equal((typed, null), Read(typed));

    [Fact]
    public void Spaces_around_the_text_and_before_the_day_are_trimmed() =>
        Assert.Equal(("pay rent", "2026-11-13"), Read("  pay rent    fri  "));

    [Fact]
    public void Empty_and_blank_text_read_as_nothing()
    {
        Assert.Equal(("", null), Read(""));
        Assert.Equal(("", null), Read("   "));
    }

    [Fact]
    public void A_lone_quoted_day_word_stays_text_without_the_quote_mark() =>
        Assert.Equal(("fri", null), Read("fri\""));
}

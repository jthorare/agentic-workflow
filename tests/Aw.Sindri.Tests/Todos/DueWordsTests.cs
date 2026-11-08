using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Tests.Todos;

/// <summary>The due-word parser (todo spec D7), with today injected: Wednesday 2026-11-11.</summary>
public sealed class DueWordsTests
{
    private static readonly DateOnly _today = new(2026, 11, 11);

    [Theory, Trait("Lane", "unit")]
    [InlineData("today", "2026-11-11")]
    [InlineData("tomorrow", "2026-11-12")]
    [InlineData("fri", "2026-11-13")]
    [InlineData("mon", "2026-11-16")]
    [InlineData("wed", "2026-11-18")]   // the next Wednesday, never today
    [InlineData("sun", "2026-11-15")]
    [InlineData("Fri", "2026-11-13")]
    [InlineData("11-20", "2026-11-20")]
    [InlineData("11-11", "2026-11-11")]   // today hasn't passed
    [InlineData("11-10", "2027-11-10")]   // passed: next year
    [InlineData("02-29", "2028-02-29")]   // the next one that exists
    [InlineData("2026-11-10", "2026-11-10")]   // a written-out date is taken as typed, past or not
    public void Resolve_reads_each_word(string word, string expected)
    {
        var read = DueWords.Resolve(word, _today);

        Assert.True(read.IsSuccess, read.IsError ? read.Error.ToString() : null);
        Assert.Equal(DateOnly.ParseExact(expected, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), read.Value);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("frday")]
    [InlineData("11-31")]
    [InlineData("2026-13-01")]
    [InlineData("")]
    public void Resolve_refuses_a_word_that_is_not_a_date_with_SND_TODO_003(string word)
    {
        var read = DueWords.Resolve(word, _today);

        Assert.True(read.IsError);
        Assert.Equal("SND-TODO-003", read.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("reply to Sam about the offsite fri", "reply to Sam about the offsite", "2026-11-13")]
    [InlineData("call the dentist tomorrow", "call the dentist", "2026-11-12")]
    [InlineData("renew the cert 11-20", "renew the cert", "2026-11-20")]
    [InlineData("pay rent 2026-12-01", "pay rent", "2026-12-01")]
    [InlineData("check flaky test FRI", "check flaky test", "2026-11-13")]
    public void Split_takes_a_trailing_due_word_off_the_text(string typed, string text, string due)
    {
        var split = DueWords.Split(typed, _today);

        Assert.True(split.IsSuccess, split.IsError ? split.Error.ToString() : null);
        Assert.Equal((text, DateOnly.ParseExact(due, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)), (split.Value.Text, split.Value.Due));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("call mon\"", "call mon")]   // a trailing quote keeps the word as text
    [InlineData("rotate the staging key", "rotate the staging key")]
    [InlineData("friday party", "friday party")]   // only a trailing word counts
    [InlineData("see the 11-10 report", "see the 11-10 report")]
    public void Split_leaves_other_text_alone_with_no_date(string typed, string text)
    {
        var split = DueWords.Split(typed, _today);

        Assert.True(split.IsSuccess, split.IsError ? split.Error.ToString() : null);
        Assert.Equal(text, split.Value.Text);
        Assert.Null(split.Value.Due);
    }
}

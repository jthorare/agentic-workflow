using System.Globalization;
using Aw.Kernel.Dates;

namespace Aw.Kernel.Tests.Dates;

/// <summary>DueWords.Resolve's written forms (issue 3240): "Dec 5", "5 Dec", "December 5", a trailing year, "next fri", and the forms it read before.</summary>
[Trait("Lane", "unit")]
public sealed class DueWordsPhraseTests
{
    private static readonly DateOnly _thursday = new(2026, 11, 12);

    [Theory]
    [InlineData("2026-12-05", "2026-12-05")]
    [InlineData(" 2026-12-05 ", "2026-12-05")]
    [InlineData("Dec 5", "2026-12-05")]
    [InlineData("dec 5", "2026-12-05")]
    [InlineData("5 Dec", "2026-12-05")]
    [InlineData("December 5", "2026-12-05")]
    [InlineData("dec 5 2026", "2026-12-05")]
    [InlineData("Dec 5, 2027", "2027-12-05")]
    [InlineData("jan 3", "2027-01-03")]
    [InlineData("nov 12", "2026-11-12")]
    [InlineData("nov 11", "2027-11-11")]
    [InlineData("today", "2026-11-12")]
    [InlineData("Tomorrow", "2026-11-13")]
    [InlineData("fri", "2026-11-13")]
    [InlineData("next fri", "2026-11-13")]
    [InlineData("Next Friday", "2026-11-13")]
    [InlineData("thu", "2026-11-19")]
    [InlineData("next mon", "2026-11-16")]
    public void A_form_people_type_reads_as_its_date(string text, string expected)
    {
        var read = DueWords.Resolve(text, _thursday);

        Assert.True(read.IsSuccess);
        Assert.Equal(DateOnly.Parse(expected, CultureInfo.InvariantCulture), read.Value);
    }

    [Theory]
    [InlineData("soonish")]
    [InlineData("2026-13-01")]
    [InlineData("03/02/2026")]
    [InlineData("dec 32")]
    [InlineData("feb 30")]
    [InlineData("next")]
    [InlineData("next dec")]
    [InlineData("dec 5 26")]
    [InlineData("")]
    [InlineData("   ")]
    public void Text_that_is_not_a_date_is_refused_with_the_todo_code(string text)
    {
        var read = DueWords.Resolve(text, _thursday);

        Assert.True(read.IsError);
        Assert.Equal("SND-TODO-003", read.Error.Code);
        Assert.Contains("Dec 5", read.Error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("fri", "2026-11-13")]
    [InlineData("thu", "2026-11-19")]
    [InlineData("11-12", "2026-11-12")]
    [InlineData("11-11", "2027-11-11")]
    [InlineData("02-29", "2028-02-29")]
    public void The_single_word_forms_read_as_before(string word, string expected) =>
        Assert.Equal(DateOnly.Parse(expected, CultureInfo.InvariantCulture), DueWords.Resolve(word, _thursday).Value);
}

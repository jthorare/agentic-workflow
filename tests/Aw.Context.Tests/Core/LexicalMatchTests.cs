using Aw.Context.Core.Records;

namespace Aw.Context.Tests.Core;

[Trait("Lane", "unit")]
public sealed class LexicalMatchTests
{
    [Fact]
    public void Each_term_is_a_quoted_phrase_joined_with_or() =>
        Assert.Equal("\"split\" OR \"pane orientation\"", LexicalMatch.Of(["split", " pane orientation "]));

    [Fact]
    public void Query_syntax_is_text_not_syntax() =>
        Assert.Equal("\"a\"\" OR \"\"b\" OR \"NEAR(x y)\" OR \"col:val\" OR \"-x\"", LexicalMatch.Of(["a\" OR \"b", "NEAR(x y)", "col:val", "*", "-x"]));

    [Fact]
    public void Terms_with_no_letter_or_digit_and_repeats_are_dropped_and_nothing_left_is_null()
    {
        Assert.Equal("\"x\"", LexicalMatch.Of(["x", "x", "", "  ", "()"]));
        Assert.Null(LexicalMatch.Of(["", "***", "\t"]));
        Assert.Null(LexicalMatch.Of([]));
    }

    [Fact]
    public void Control_characters_become_spaces_and_the_term_and_term_count_are_capped()
    {
        Assert.Equal("\"a b\"", LexicalMatch.Of(["a\nb"]));
        Assert.Equal(LexicalMatch.MaxTerms, LexicalMatch.Of([.. Enumerable.Range(0, 100).Select(i => new string('t', i + 1))])!.Split(" OR ", StringSplitOptions.None).Length);
        Assert.Equal(LexicalMatch.MaxTermLength + 2, LexicalMatch.Of([new string('z', 1000)])!.Length);
    }
}

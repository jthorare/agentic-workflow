using Aw.Judge.Core.Questions;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Questions;

[Lane(TestLane.Unit)]
public sealed class UntrustedTextTests
{
    [Fact]
    public void Text_over_the_cap_is_cut_with_a_visible_marker()
    {
        Assert.Equal("abc", UntrustedText.Capped("abc", 3));
        Assert.Equal("ab\n[truncated 2 chars]", UntrustedText.Capped("abcd", 2));
    }

    [Fact]
    public void Delimiter_tags_are_renamed_in_any_case() =>
        Assert.Equal("<brief-text> </DIFF-text> <problem-text", UntrustedText.Neutralized("<brief> </DIFF> <problem"));

    [Fact]
    public void A_labelled_field_becomes_one_line() =>
        Assert.Equal("a b c", UntrustedText.OneLine("a \r\n b\u2028c"));
}

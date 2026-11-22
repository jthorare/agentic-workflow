using Aw.Meta.Core.Delegations;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Delegations;

public sealed class DelegationBookTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_book_round_trips_and_a_tab_in_the_title_becomes_a_space()
    {
        var first = new Delegation(5, "https://x/5", "Fix\tthe board", "api", "t1-api", A.At(1));
        var second = new Delegation(6, "https://x/6", "Theme", "ui", "t1-ui", A.At(2)) { Delivered = true, Claude = false };

        var read = DelegationBook.Parse(DelegationBook.Format([first, second]));

        Assert.Equal([first with { Title = "Fix the board" }, second], read);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_line_that_does_not_read_is_skipped()
    {
        var good = DelegationBook.Format([new Delegation(5, "u", "t", "api", "t1-api", A.At(1))]);

        _ = Assert.Single(DelegationBook.Parse("junk\n" + good + "\nx\ty"));
        Assert.Empty(DelegationBook.Parse(stored: null));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("ACTION: #77 x", 77, true)]
    [InlineData("done (#77)", 77, true)]
    [InlineData("#770", 77, false)]
    [InlineData("#7 and #77", 77, true)]
    [InlineData("77", 77, false)]
    public void Names_matches_the_issue_marker_with_no_digit_after(string text, int issue, bool expected) =>
        Assert.Equal(expected, DelegationText.Names(text, issue));

    [Theory, Trait("Lane", "unit")]
    [InlineData("Dark THEME toggle", "theme", true)]
    [InlineData("theming", "theme", false)]
    [InlineData("the_theme", "theme", false)]
    [InlineData("fix c# build", "c#", true)]
    [InlineData("anything", " ", false)]
    public void HasWord_matches_a_whole_word_in_any_case(string text, string keyword, bool expected) =>
        Assert.Equal(expected, DelegationText.HasWord(text, keyword));
}

using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The question text of an asking session (Agents page, issue 3082): the last 3 non-empty lines.</summary>
public sealed class PaneQuestionTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_question_is_the_last_three_non_empty_lines_joined_by_line_feeds()
    {
        Assert.Equal("c\nd\ne", PaneQuestion.Of(["a", "b", "", "c", "  ", "d", "e", "", ""]));
    }

    [Fact, Trait("Lane", "unit")]
    public void Fewer_than_three_lines_are_all_kept()
    {
        Assert.Equal("only", PaneQuestion.Of(["", "only", ""]));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pane_with_no_text_has_no_question()
    {
        Assert.Null(PaneQuestion.Of(["", "   ", ""]));
        Assert.Null(PaneQuestion.Of([]));
    }
}

using Aw.BugFix.Core.Model;

namespace Aw.BugFix.Tests.Rules;

/// <summary>Which commit messages and ticket ids name a GitHub issue (#1189).</summary>
public sealed class GithubIssueTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("#1189", 1189)]
    [InlineData("1189", 1189)]
    [InlineData("jthorare/agentic-workflow#1189", 1189)]
    [InlineData("https://github.com/jthorare/agentic-workflow/issues/1189", 1189)]
    public void A_github_ticket_id_gives_the_issue_number(string reference, int expected) =>
        Assert.Equal(expected, GithubIssue.Number(new Ticket(TicketSource.Github, reference, "t", "b", "e", "a")));

    [Theory, Trait("Lane", "unit")]
    [InlineData(TicketSource.Linear, "ENG-1189")]
    [InlineData(TicketSource.Text, null)]
    [InlineData(TicketSource.Github, null)]
    [InlineData(TicketSource.Github, "no number")]
    public void Any_other_ticket_has_no_issue_number(TicketSource source, string? reference) =>
        Assert.Null(GithubIssue.Number(new Ticket(source, reference, "t", "b", "e", "a")));

    [Theory, Trait("Lane", "unit")]
    [InlineData("fix: cookie\n\nPart of #1189", true)]
    [InlineData("fix: cookie (Fixes #1189)", true)]
    [InlineData("fix: cookie\n\npart of jthorare/agentic-workflow#1189", true)]
    [InlineData("fix: cookie\n\nPart of #118", false)]
    [InlineData("fix: cookie\n\nPart of #11890", false)]
    [InlineData("fix: cookie\n\nPart of #1190", false)]
    [InlineData("fix: cookie, see #1189", false)]
    public void A_message_names_the_issue_only_by_part_of_or_fixes(string message, bool expected) =>
        Assert.Equal(expected, GithubIssue.Names(message, 1189));
}

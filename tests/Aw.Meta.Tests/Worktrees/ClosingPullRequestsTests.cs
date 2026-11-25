using Aw.Meta.Core.Worktrees;

namespace Aw.Meta.Tests.Worktrees;

/// <summary>Issue 2753: the GraphQL answer about a bugfix run's issue. Only a CLOSED issue with a MERGED closing pull request counts; anything else, or an answer that can't be read, is none.</summary>
public sealed class ClosingPullRequestsTests
{
    private static string Answer(string state, string nodes) =>
        "{\"data\":{\"repository\":{\"issue\":{\"state\":\"" + state + "\",\"closedByPullRequestsReferences\":{\"nodes\":[" + nodes + "]}}}}}";

    [Fact, Trait("Lane", "unit")]
    public void A_closed_issue_gives_its_merged_closing_pull_requests_only() =>
        Assert.Equal([2720], ClosingPullRequests.Merged(Answer("CLOSED", "{\"number\":2711,\"state\":\"CLOSED\"},{\"number\":2720,\"state\":\"MERGED\"}")));

    [Theory, Trait("Lane", "unit")]
    [InlineData("OPEN", "{\"number\":2720,\"state\":\"MERGED\"}")]
    [InlineData("CLOSED", "{\"number\":2720,\"state\":\"OPEN\"}")]
    [InlineData("CLOSED", "")]
    public void An_open_issue_or_no_merged_closer_gives_none(string state, string nodes) =>
        Assert.Empty(ClosingPullRequests.Merged(Answer(state, nodes)));

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("{ not json")]
    [InlineData("{\"data\":{\"repository\":{\"issue\":null}}}")]
    [InlineData("{\"errors\":[{\"message\":\"Could not resolve to an Issue\"}]}")]
    public void An_answer_that_cannot_be_read_gives_none(string json) =>
        Assert.Empty(ClosingPullRequests.Merged(json));
}

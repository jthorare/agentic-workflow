using Aw.Connectors.Testing.GitHub;
using Aw.Connectors.Testing.Slack;

namespace Aw.Connectors.Testing;

/// <summary>The builder root: A.&lt;Slice&gt;Request() and the like, with deterministic defaults (spec §4b.5).</summary>
public static class A
{
    /// <summary>A PullGitHubRequest builder with deterministic defaults.</summary>
    /// <returns>A new builder.</returns>
    public static PullGitHubRequestBuilder PullGitHubRequest() => new();

    /// <summary>A GitHub issue (or pull request) builder with deterministic defaults.</summary>
    /// <param name="id">GitHub's global id; the number defaults to it.</param>
    /// <returns>A new builder.</returns>
    public static GitHubIssueBuilder GitHubIssue(long id) => new(id);

    /// <summary>A GitHub comment builder with deterministic defaults.</summary>
    /// <param name="id">GitHub's global id of the comment.</param>
    /// <returns>A new builder.</returns>
    public static GitHubCommentBuilder GitHubComment(long id) => new(id);

    /// <summary>A Slack message builder with deterministic defaults.</summary>
    /// <param name="ts">The message's ts ('1700000000.000100').</param>
    /// <returns>A new builder.</returns>
    public static SlackMessageBuilder SlackMessage(string ts) => new(ts);
}

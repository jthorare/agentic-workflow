namespace Aw.Connectors.Core.GitHub;

/// <summary>One comment on an issue or pull request (the conversation tab; review comments on the diff are not read).</summary>
/// <param name="Id">GitHub's global id of the comment.</param>
/// <param name="IssueNumber">The number of the issue or pull request it is on.</param>
/// <param name="Author">The author's login, or null for a deleted account.</param>
/// <param name="Body">The body as written (markdown).</param>
/// <param name="CreatedAt">When it was written.</param>
/// <param name="UpdatedAt">When it last changed (the order key of the repo-wide read).</param>
public sealed record GitHubComment(long Id, int IssueNumber, string? Author, string Body, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

namespace Aw.Connectors.Core.GitHub;

/// <summary>One entry of a repo's issue list; GitHub returns pull requests in the same list (<see cref="IsPullRequest"/>).</summary>
/// <param name="Id">GitHub's global id of the issue or pull request (the cursor tells entries at one instant apart by it).</param>
/// <param name="Number">The number within the repo.</param>
/// <param name="IsPullRequest">True when the entry has a pull_request field.</param>
/// <param name="Title">The title.</param>
/// <param name="Body">The body as written (markdown); empty when none.</param>
/// <param name="State">GitHub's state, 'open' or 'closed'.</param>
/// <param name="Author">The author's login, or null for a deleted account.</param>
/// <param name="Url">The page url.</param>
/// <param name="CreatedAt">When it was opened.</param>
/// <param name="UpdatedAt">When it last changed (the order and cursor key).</param>
/// <param name="ClosedAt">When it was closed, or null.</param>
/// <param name="Labels">The label names.</param>
/// <param name="Comments">How many comments it has.</param>
public sealed record GitHubIssue(
    long Id,
    int Number,
    bool IsPullRequest,
    string Title,
    string Body,
    string State,
    string? Author,
    string Url,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ClosedAt,
    IReadOnlyList<string> Labels,
    int Comments);

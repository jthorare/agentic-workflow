namespace Aw.Sindri.Core.Reviews;

/// <summary>An open stored PR record of a synced repo, with what the Reviews list needs of it (PR review page spec §5.6, §6.1).</summary>
/// <param name="Repo">owner/name.</param>
/// <param name="Number">The PR number.</param>
/// <param name="Title">The title.</param>
/// <param name="Author">The author's login.</param>
/// <param name="Draft">Whether it is a draft.</param>
/// <param name="OpenedAt">When it opened.</param>
/// <param name="UpdatedAt">Its newest activity.</param>
/// <param name="HeadSha">The head commit, or null when the source gave none.</param>
/// <param name="RequestedReviewers">The logins whose review is requested.</param>
/// <param name="Reviews">Each reviewer's latest review.</param>
/// <param name="ReviewersRead">Whether Sindri has read this record's reviewers yet.</param>
/// <param name="Claims">The refs of the PR's Covers: line.</param>
/// <param name="Url">The PR's link on the forge, which the forge port takes; null when the record has none.</param>
public sealed record ReviewPull(
    string Repo,
    int Number,
    string Title,
    string Author,
    bool Draft,
    DateTimeOffset OpenedAt,
    DateTimeOffset UpdatedAt,
    string? HeadSha,
    IReadOnlyList<string> RequestedReviewers,
    IReadOnlyList<ReviewedAt> Reviews,
    bool ReviewersRead,
    IReadOnlyList<ReviewClaim> Claims,
    string? Url = null);

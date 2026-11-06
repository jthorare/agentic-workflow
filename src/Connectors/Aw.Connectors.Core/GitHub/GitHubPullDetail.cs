namespace Aw.Connectors.Core.GitHub;

/// <summary>What the single-pull-request route says that the issue list does not.</summary>
/// <param name="Merged">True when the pull request was merged.</param>
/// <param name="Draft">True when it is a draft.</param>
/// <param name="MergedAt">When it was merged, or null.</param>
/// <param name="Paths">The paths it changed, at most 300 (three pages of 100); a renamed file lists its new path.</param>
/// <param name="RequestedReviewers">The logins whose review is still requested ('requested_reviewers'; a team request is not a login and is left out); null reads as none.</param>
/// <param name="HeadSha">The head commit ('head.sha'), or null when the reply had none.</param>
public sealed record GitHubPullDetail(bool Merged, bool Draft, DateTimeOffset? MergedAt, IReadOnlyList<string> Paths, IReadOnlyList<string>? RequestedReviewers = null, string? HeadSha = null);

namespace Aw.Connectors.Core;

/// <summary>One reviewer's latest review of a pull request and how far the pull request has moved since.</summary>
/// <param name="Login">The reviewer's login.</param>
/// <param name="Commit">The commit their latest review was on ('commit_id').</param>
/// <param name="NewCommits">How many commits the head is ahead of <paramref name="Commit"/> (0 when the review is on the head); null when that is unknown, such as a force-push that dropped the commit.</param>
/// <param name="State">The reviewer's latest verdict (APPROVED or CHANGES_REQUESTED); COMMENTED only when they gave none; null when the reply had none.</param>
public sealed record ConnectorReview(string Login, string Commit, int? NewCommits, string? State = null);

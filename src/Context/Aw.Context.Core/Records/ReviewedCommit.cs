namespace Aw.Context.Core.Records;

/// <summary>One reviewer's latest review of a pull request.</summary>
/// <param name="Login">The reviewer's login.</param>
/// <param name="Commit">The commit the review was on.</param>
/// <param name="NewCommits">How many commits the head is ahead of <paramref name="Commit"/>; null when unknown (a force-push dropped the commit).</param>
/// <param name="State">The reviewer's latest verdict (APPROVED or CHANGES_REQUESTED); COMMENTED only when they gave none; null for a record stored before the state was kept, which reads as unknown (#4333).</param>
public sealed record ReviewedCommit(string Login, string Commit, int? NewCommits, string? State = null);

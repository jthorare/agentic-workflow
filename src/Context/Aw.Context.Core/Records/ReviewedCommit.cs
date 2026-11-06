namespace Aw.Context.Core.Records;

/// <summary>One reviewer's latest review of a pull request.</summary>
/// <param name="Login">The reviewer's login.</param>
/// <param name="Commit">The commit the review was on.</param>
/// <param name="NewCommits">How many commits the head is ahead of <paramref name="Commit"/>; null when unknown (a force-push dropped the commit).</param>
public sealed record ReviewedCommit(string Login, string Commit, int? NewCommits);

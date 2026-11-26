namespace Aw.Sindri.Core.Reviews;

/// <summary>A reviewer's latest review of a PR (PR review page spec §5.6).</summary>
/// <param name="Login">The reviewer's login.</param>
/// <param name="Commit">The commit the review was on.</param>
/// <param name="NewCommits">The commits pushed since, or null when it is unknown (a force-push dropped the reviewed commit).</param>
/// <param name="State">The review's state as GitHub gives it (APPROVED, CHANGES_REQUESTED, COMMENTED); null when the record was stored before the state was kept, which reads as unknown (#4333).</param>
public sealed record ReviewedAt(string Login, string Commit, int? NewCommits, string? State = null);

namespace Aw.Sindri.Core.Reviews;

/// <summary>One PR on the Reviews list (PR review page spec §6.1).</summary>
/// <param name="Repo">owner/name.</param>
/// <param name="Number">The PR number.</param>
/// <param name="Title">The title.</param>
/// <param name="Author">The author's login.</param>
/// <param name="Draft">Whether it is a draft.</param>
/// <param name="OpenedAt">When it opened.</param>
/// <param name="LastActivity">Its newest activity.</param>
/// <param name="Waiting">Why it waits on the owner; null in Yours.</param>
/// <param name="WaitingText">"review requested", "reviewed, 2 new commits" or "reviewed, new commits"; null in Yours.</param>
/// <param name="NewCommits">The commits since the owner's review; null when unknown or not waiting on one.</param>
/// <param name="Ci">The CI roll-up from the per-head summary.</param>
/// <param name="RequiredOpen">The open required checks; null when RequiredState is notRead or noneNamed.</param>
/// <param name="RequiredState">Why <paramref name="RequiredOpen"/> is blank, or that it counts: one of <see cref="ReviewRequiredState"/>.</param>
/// <param name="RequiredText">"2 open" or "none open"; null with <paramref name="RequiredOpen"/>.</param>
/// <param name="ClaimsDoc">Whether the PR names a design doc.</param>
/// <param name="Href">The page model's path.</param>
public sealed record ReviewRow(
    string Repo,
    int Number,
    string Title,
    string Author,
    bool Draft,
    DateTimeOffset OpenedAt,
    DateTimeOffset LastActivity,
    ReviewWaitingKind? Waiting,
    string? WaitingText,
    int? NewCommits,
    ReviewCi Ci,
    int? RequiredOpen,
    string RequiredState,
    string? RequiredText,
    bool ClaimsDoc,
    string Href);

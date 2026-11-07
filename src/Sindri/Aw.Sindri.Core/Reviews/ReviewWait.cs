namespace Aw.Sindri.Core.Reviews;

/// <summary>Why a PR waits on the owner.</summary>
/// <param name="Kind">The reason.</param>
/// <param name="Text">"review requested", "reviewed, 2 new commits" or "reviewed, new commits".</param>
/// <param name="NewCommits">The commits since the owner's review; null when unknown or not that reason.</param>
public sealed record ReviewWait(ReviewWaitingKind Kind, string Text, int? NewCommits);

namespace Aw.Meta.Features.Watch;

/// <summary>The facts besides the pull request list that decide which pull requests the team owns.</summary>
/// <param name="Queued">The numbers in merge.queued.</param>
/// <param name="Ready">The numbers any lane sent as "ready #N".</param>
/// <param name="CheckedOut">The branches checked out in a worktree under the team's roots.</param>
internal sealed record TeamPrFacts(IReadOnlySet<int> Queued, IReadOnlySet<int> Ready, IReadOnlySet<string> CheckedOut);

namespace Aw.Sindri.Features.HomeLanes;

/// <summary>One entry of 'aw-meta status --json' mergeQueue[]: a lane sent 'ready #N' for this PR.</summary>
/// <param name="Pr">The PR number.</param>
/// <param name="From">The lane's session name.</param>
/// <param name="Github">'queued N' when the PR sits in GitHub's merge queue; null otherwise.</param>
internal sealed record QueuedPr(int Pr, string From, string? Github);

namespace Aw.Sindri.Core.Home;

/// <summary>One merge-queue entry ('aw-meta status --json' mergeQueue[]).</summary>
/// <param name="Pr">The pull request number.</param>
/// <param name="From">The lane's tmux session name that sent 'ready'.</param>
/// <param name="Gate">The latest gate verdict, or 'none'.</param>
/// <param name="Github">What GitHub's merge queue says ('queued 2 (AWAITING_CHECKS)'), or null when meta has not handed it there.</param>
public sealed record MetaQueuedPr(int Pr, string From, string Gate, string? Github);

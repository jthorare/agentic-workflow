namespace Aw.Meta.Core.Github;

/// <summary>What GitHub says about a pull request and the base branch's merge queue.</summary>
/// <param name="Enabled">Whether the base branch has a merge queue.</param>
/// <param name="InQueue">Whether the pull request is in the queue now.</param>
/// <param name="Position">Its place in the queue (1 is next), when it is in the queue.</param>
/// <param name="EntryState">The queue entry's state (QUEUED, AWAITING_CHECKS, MERGEABLE, UNMERGEABLE, LOCKED), when it is in the queue.</param>
/// <param name="RemovedReason">The reason of the latest "removed from the merge queue" event, when there is one.</param>
public sealed record MergeQueueView(bool Enabled, bool InQueue, int? Position, string? EntryState, string? RemovedReason)
{
    /// <summary>Whether the pull request is part of a stack on GitHub (a stack made by the gh stack extension); such a pull request merges only with "gh stack merge".</summary>
    public bool Stacked { get; init; }
}

using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Messages;

namespace Aw.Meta.Core.Obligations;

/// <summary>Everything the open set is derived from.</summary>
public sealed record ObligationFacts
{
    /// <summary>Every inbound message.</summary>
    public required IReadOnlyList<InboxMessage> Messages { get; init; }

    /// <summary>Every message the meta sent.</summary>
    public required IReadOnlyList<Outbound> Outbound { get; init; }

    /// <summary>The current lane map.</summary>
    public required LaneMapState Lanes { get; init; }

    /// <summary>The meta lane's name; it is never sent broadcasts.</summary>
    public required string MetaLane { get; init; }

    /// <summary>The latest merge, if any.</summary>
    public MergeRecord? LatestMerge { get; init; }

    /// <summary>Owner answers recorded so far.</summary>
    public IReadOnlyList<OwnerAnswer> Answers { get; init; } = [];

    /// <summary>When the watch last beat; null when no watch ever started.</summary>
    public DateTimeOffset? Heartbeat { get; init; }

    /// <summary>The watch's poll interval.</summary>
    public TimeSpan WatchInterval { get; init; } = TimeSpan.FromSeconds(120);

    /// <summary>The meta lane's measured context; null when it could not be measured.</summary>
    public LaneContext? MetaContext { get; init; }

    /// <summary>True when a detached recycle waiter is recorded for the meta lane.</summary>
    public bool MetaWaiter { get; init; }

    /// <summary>The current time.</summary>
    public required DateTimeOffset Now { get; init; }
}

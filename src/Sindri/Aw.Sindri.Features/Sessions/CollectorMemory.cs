using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.Sessions;

/// <summary>
/// What the collector keeps between passes (a singleton; the host's loop is its only writer): the change rules' state, the last good pass's sessions and
/// the recently ended ones. Live state is memory only (collector spec CO3); the ledger gets the changes.
/// </summary>
internal sealed class CollectorMemory
{
    internal static readonly TimeSpan EndedFor = TimeSpan.FromMinutes(10);

    /// <summary>The most changes kept while the ledger cannot take them; past it the oldest go.</summary>
    internal const int PendingCap = 1000;

    internal bool PendingOverflowLogged { get; set; }

    internal FleetTrackerState Tracker { get; set; } = FleetTrackerState.Empty;

    /// <summary>Changes found but not yet appended to the ledger, oldest first: an append that fails leaves them here for the next pass. Past <see cref="PendingCap"/> the oldest are dropped, which can orphan a 'gone' whose 'appeared' went first.</summary>
    internal IReadOnlyList<FleetChange> Pending { get; set; } = [];

    /// <summary>State changes found but not yet appended as 'session.changed', oldest first (the same wait as <see cref="Pending"/>).</summary>
    internal IReadOnlyList<SessionStateChange> PendingStates { get; set; } = [];

    /// <summary>Holds each session's last emitted state and the new one waiting to hold 5 s, so 'session.changed' skips a flip that does not last.</summary>
    internal SessionStateDebounce States { get; } = new();

    internal bool Restored { get; set; }

    internal IReadOnlyDictionary<SessionRef, ObservedSession> Previous { get; set; } = new Dictionary<SessionRef, ObservedSession>();

    internal IReadOnlyList<EndedSession> Ended { get; set; } = [];

    /// <summary>Each live session's last state changes in this run (collector spec CA-2).</summary>
    internal IReadOnlyDictionary<SessionRef, IReadOnlyList<SessionStateEntry>> History { get; set; } = new Dictionary<SessionRef, IReadOnlyList<SessionStateEntry>>();

    /// <summary>The sessions that left this good pass join the ended list; the ones that returned leave it; the ones older than ten minutes are dropped.</summary>
    internal IReadOnlyList<EndedSession> Ending(IReadOnlyList<ObservedSession> now, DateTimeOffset at)
    {
        var live = now.Select(static s => s.Ref).ToHashSet();
        var gone = Previous.Values.Where(s => !live.Contains(s.Ref)).Select(s => new EndedSession(s, at));
        return [.. Ended.Where(e => !live.Contains(e.Session.Ref)).Concat(gone).Where(e => at - e.EndedAt <= EndedFor).OrderByDescending(static e => e.EndedAt)];
    }
}

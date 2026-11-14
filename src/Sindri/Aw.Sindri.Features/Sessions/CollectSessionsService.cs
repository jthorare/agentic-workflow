using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Sessions;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Features.Sessions;

/// <summary>
/// One pass of the session collector (collector spec §4.1): discovers the sessions, merges their states, publishes the snapshot and works out which changes are worth
/// a ledger row. It writes nothing but the in-memory snapshot and, after a good pass, prunes dead sessions' hook files.
/// </summary>
internal sealed partial class CollectSessionsService(SessionDiscovery discovery, SessionObserver observer, CollectorMemory memory, ISessionSnapshot snapshot, TimeProvider clock, ILogger<CollectSessionsService> logger)
    : IService<CollectSessionsRequest, CollectSessionsResponse>
{
    public async ValueTask<Result<CollectSessionsResponse>> Handle(CollectSessionsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = clock.GetUtcNow();
        if (!discovery.IsEnabled())
        {
            memory.History = new Dictionary<SessionRef, IReadOnlyList<SessionStateEntry>>();
            Publish([], [], [SessionProblems.Disabled], now);
            return new CollectSessionsResponse(memory.Pending, Good: false, Sessions: 0, Pruned: 0) { StateChanges = memory.PendingStates };
        }

        var found = await Discover(request.Budget, cancellationToken).ConfigureAwait(false);
        if (found is null || !found.Good)
        {
            KeepPrevious([.. found?.Problems ?? [SessionProblems.Overrun]], now);
            return new CollectSessionsResponse(memory.Pending, Good: false, Sessions: 0, Pruned: 0) { Overran = found is null, StateChanges = memory.PendingStates };
        }

        var sessions = Observe(found.Candidates, now);
        sessions = KeepUnknown(sessions, found.Unknown);
        var diff = FleetChangeRules.Diff(memory.Tracker, sessions, now, goodPass: true);
        var ended = memory.Ending(sessions, now);
        memory.Tracker = diff.State;
        memory.PendingStates = CappedStates([.. memory.PendingStates, .. SessionStateChanges.Diff(memory.Previous, sessions, now)]);
        memory.Pending = Capped([.. memory.Pending, .. diff.Changes]);   // kept until the ledger has them (RecordFleetChangesService clears what it appended)
        LogHostChanges(sessions);
        memory.Previous = sessions.ToDictionary(static s => s.Ref);
        memory.Ended = ended;
        memory.History = StateHistoryRules.Next(memory.History, sessions);
        Publish(sessions, ended, found.Problems, now);
        return new CollectSessionsResponse(memory.Pending, Good: true, sessions.Count, discovery.Prune([.. sessions.Select(static s => s.Ref)], now)) { StateChanges = memory.PendingStates };
    }

    /// <summary>A ledger that stays unwritable can't grow the queue without bound: past the cap the oldest changes are dropped, and the first drop is logged.</summary>
    private List<FleetChange> Capped(List<FleetChange> pending)
    {
        if (pending.Count <= CollectorMemory.PendingCap)
        {
            memory.PendingOverflowLogged &= pending.Count >= CollectorMemory.PendingCap;   // logged again if it overflows again later
            return pending;
        }

        if (!memory.PendingOverflowLogged)
        {
            memory.PendingOverflowLogged = true;
            LogOverflow(logger, CollectorMemory.PendingCap);
        }

        return pending[^CollectorMemory.PendingCap..];
    }

    /// <summary>The state changes wait the same way and are capped the same way as the fleet changes; the oldest go first.</summary>
    private static List<SessionStateChange> CappedStates(List<SessionStateChange> pending) => pending.Count <= CollectorMemory.PendingCap ? pending : pending[^CollectorMemory.PendingCap..];

    [LoggerMessage(Level = LogLevel.Warning, Message = "The session collector holds {Cap} fleet changes the ledger hasn't taken; the oldest are dropped from here on")]
    private static partial void LogOverflow(ILogger logger, int cap);

    /// <summary>One debug line per session whose host kind changed (refs and kinds only: no socket path, no cwd, no title).</summary>
    private void LogHostChanges(List<ObservedSession> sessions)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        foreach (var session in sessions.Where(s => HostChanged(memory.Previous.GetValueOrDefault(s.Ref)?.Host, s.Host)))
        {
            LogHost(logger, session.Ref, session.Host.Kind, session.Host.Tmux?.SocketKind ?? "-");
        }
    }

    private static bool HostChanged(HostInfo? before, HostInfo now) =>
        before?.Kind != now.Kind || before?.Tmux?.SocketKind != now.Tmux?.SocketKind;

    [LoggerMessage(Level = LogLevel.Debug, Message = "session {Ref} host {Kind} tmux socket kind {SocketKind}")]
    private static partial void LogHost(ILogger logger, SessionRef @ref, string kind, string socketKind);

    private async Task<Discovered?> Discover(TimeSpan budget, CancellationToken cancellationToken)
    {
        try
        {
            return await Task.Run(() => discovery.Discover(budget, cancellationToken), cancellationToken).ConfigureAwait(false);   // null: the process table overran
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new Discovered([], [], Good: false);   // shutting down: no pass, no problem row, nothing published as a failure
        }
    }

    private List<ObservedSession> Observe(IReadOnlyList<Candidate> candidates, DateTimeOffset now) =>
    [
        .. candidates.GroupBy(static c => c.Ref).Select(static g => g.First())
            .Select(c => observer.Observe(c, now, memory.Previous.GetValueOrDefault(c.Ref)))
            .OrderBy(static s => s.Ref.ToString(), StringComparer.Ordinal),
    ];

    /// <summary>
    /// A process that couldn't be classified this pass (its environment was unreadable) is shown exactly as last pass showed it, whichever path built it this time: no 'gone',
    /// no 'appeared', no lost lane or launch. One never shown before stays out until a pass can read it. A reused pid (other start ticks) inherits nothing.
    /// </summary>
    private List<ObservedSession> KeepUnknown(List<ObservedSession> sessions, IReadOnlyCollection<(int Pid, long StartTicks)> unknown)
    {
        if (unknown.Count == 0)
        {
            return sessions;
        }

        var kept = memory.Previous.Values.Where(p => unknown.Contains((p.Pid, p.StartTicks))).ToList();
        return [.. sessions.Where(s => !unknown.Contains((s.Pid, s.StartTicks))).Concat(kept).OrderBy(static s => s.Ref.ToString(), StringComparer.Ordinal)];
    }

    private void KeepPrevious(List<SessionProblem> problems, DateTimeOffset now)
    {
        var last = snapshot.Current;
        Publish(last?.Sessions ?? [], last?.RecentlyEnded ?? [], problems, last?.ObservedAt ?? now);
    }

    private void Publish(IReadOnlyList<ObservedSession> sessions, IReadOnlyList<EndedSession> ended, IReadOnlyList<SessionProblem> problems, DateTimeOffset at) =>
        snapshot.Publish(new SessionSnapshot(sessions, ended, [.. discovery.StaticProblems(), .. problems], at) { History = memory.History });
}

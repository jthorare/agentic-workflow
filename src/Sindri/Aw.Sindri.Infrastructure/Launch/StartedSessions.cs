using System.Collections.Concurrent;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The one-off sessions this process started, by act id (agents spec §15.8): the stored answer of a 2xx start and the worktree it made. The 'agent.op' event keyed by the act id is the durable copy; this
/// map answers a replay and a worktree remove without reading the ledger, and a miss after a restart reads it back once. It reads the ledger, so the port is Core.Launch's IStartedSessions and this adapter lives here (a Core.Launch -> Ledger edge closes ARCH-CYCLE-001; a Features home would cross slices, AW1003).
/// </summary>
/// <param name="ledger">Reads the event back after a restart.</param>
/// <param name="events">Reads the payload.</param>
internal sealed class StartedSessions(ILedgerStore ledger, EventFactory events) : IStartedSessions
{
    private const int Page = 500;

    private readonly ConcurrentDictionary<string, StartedEntry> _byAct = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.Ordinal);

    /// <summary>Holds the act id's gate: two calls with one act id run one after the other, so the second sees the first's result. Dispose releases it.</summary>
    public async ValueTask<IDisposable> Enter(ActId actId, CancellationToken cancellationToken)
    {
        var gate = _gates.GetOrAdd(actId.Value, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Release(gate);
    }

    /// <summary>The 2xx start stored under <paramref name="actId"/> in this process, or null.</summary>
    public StartedEntry? InMemory(ActId actId) => _byAct.GetValueOrDefault(actId.Value);

    /// <summary>Stores the 2xx start under the act id.</summary>
    /// <param name="actId">The act id.</param>
    /// <param name="entry">What the start left behind.</param>
    public void Keep(ActId actId, StartedEntry entry) => _byAct[actId.Value] = entry;

    /// <summary>The start stored under <paramref name="actId"/>: this process's, else the ledger's 'agent.op' event.</summary>
    public async ValueTask<StartedEntry?> Find(ActId actId, CancellationToken cancellationToken)
    {
        if (InMemory(actId) is { } known)
        {
            return known;
        }

        var key = AgentOpRecorded.KeyFor(actId);
        var after = 0L;
        while (true)
        {
            var read = await ledger.ReadAfter(after, Page, cancellationToken).ConfigureAwait(false);
            if (read.IsError || read.Value.Count == 0)
            {
                return null;
            }

            foreach (var recorded in read.Value)
            {
                if (StartOf(recorded, key) is { } entry)
                {
                    Keep(actId, entry);
                    return entry;
                }
            }

            after = read.Value[^1].Sequence;
        }
    }

    private StartedEntry? StartOf(LedgerEvent recorded, string key) =>
        string.Equals(recorded.IdempotencyKey, key, StringComparison.Ordinal) && string.Equals(recorded.Envelope.Type, EventTypes.AgentOp, StringComparison.Ordinal)
            && events.Payload<AgentOpRecorded>(recorded) is { IsSuccess: true } payload && payload.Value.Op == "start"
            && TmuxPaneId.TryFrom(payload.Value.Outcome, out var pane)   // an outcome that is no pane id is no stored answer
            ? new StartedEntry(pane, payload.Value.Worktree)
            : null;

    private sealed class Release(SemaphoreSlim gate) : IDisposable
    {
        public void Dispose() => _ = gate.Release();
    }
}

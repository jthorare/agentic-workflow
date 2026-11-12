using System.Collections.Concurrent;
using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.StartSession;

/// <summary>
/// The one-off sessions this process started, by act id (agents spec §15.8): the stored answer of a 2xx start and the worktree it made. The 'agent.op' event keyed by the act id is the durable copy; this
/// map answers a replay and a worktree remove without reading the ledger, and a miss after a restart reads it back once.
/// </summary>
/// <param name="ledger">Reads the event back after a restart.</param>
/// <param name="events">Reads the payload.</param>
internal sealed class StartedSessions(ILedgerStore ledger, EventFactory events)
{
    private const int Page = 500;

    private readonly ConcurrentDictionary<string, StartedEntry> _byAct = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.Ordinal);

    internal static string Key(string actId) => $"{EventTypes.AgentOp}:{actId}";

    /// <summary>Holds the act id's gate: two calls with one act id run one after the other, so the second sees the first's result. Dispose releases it.</summary>
    internal async ValueTask<IDisposable> Enter(string actId, CancellationToken cancellationToken)
    {
        var gate = _gates.GetOrAdd(actId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Release(gate);
    }

    /// <summary>The 2xx start stored under <paramref name="actId"/> in this process, or null.</summary>
    internal StartedEntry? InMemory(string actId) => _byAct.GetValueOrDefault(actId);

    internal void Keep(string actId, StartedEntry entry) => _byAct[actId] = entry;

    /// <summary>The start stored under <paramref name="actId"/>: this process's, else the ledger's 'agent.op' event.</summary>
    internal async ValueTask<StartedEntry?> Find(string actId, CancellationToken cancellationToken)
    {
        if (InMemory(actId) is { } known)
        {
            return known;
        }

        var key = Key(actId);
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
                if (string.Equals(recorded.IdempotencyKey, key, StringComparison.Ordinal) && string.Equals(recorded.Envelope.Type, EventTypes.AgentOp, StringComparison.Ordinal)
                    && events.Payload<AgentOpRecorded>(recorded) is { IsSuccess: true } payload && payload.Value.Op == "start")
                {
                    var entry = new StartedEntry(payload.Value.Outcome, payload.Value.Worktree);
                    Keep(actId, entry);
                    return entry;
                }
            }

            after = read.Value[^1].Sequence;
        }
    }

    private sealed class Release(SemaphoreSlim gate) : IDisposable
    {
        public void Dispose() => _ = gate.Release();
    }
}

/// <summary>What a 2xx start left behind.</summary>
/// <param name="Pane">The pane id the answer carried.</param>
/// <param name="Worktree">The worktree folder the start made, or null when it made none.</param>
internal sealed record StartedEntry(string Pane, string? Worktree);

using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Features.RetireInstancesOnStart;

/// <summary>
/// Gives every instance the removed recipe engine left behind one 'workflow.retired' note. The ledger is the only source: an instance is
/// every address a 'workflow.started' event names whose last 'workflow.started' or 'workflow.transition' did not enter an end state of its recipe (RetiredRecipeEnds), and one with a note already is skipped (the note's key is per instance, so a restart
/// adds nothing). An unknown or ambiguous state counts as open.
/// It runs once: it ends with a 'workflow.retire.done' marker (even when it retired nothing), and a ledger without any 'workflow.started' row gets no marker and no page read, and only recipe-shaped addresses ('{item}/{recipe}/{attempt}') are considered, so a graph run is never retired.
/// </summary>
internal sealed class RetireInstancesOnStartService(ILedgerStore ledger, HeldLease held, EventFactory events) : IService<RetireInstancesOnStartRequest, RetireInstancesOnStartResponse>
{
    private const int PageSize = 1000;

    public async ValueTask<Result<RetireInstancesOnStartResponse>> Handle(RetireInstancesOnStartRequest request, CancellationToken cancellationToken)
    {
        var grant = held.Current;
        if (grant is null)
        {
            return new RetireInstancesOnStartResponse(0);
        }

        var skip = await HasNothingToDo(cancellationToken).ConfigureAwait(false);
        if (skip.IsError || skip.Value)
        {
            return skip.IsError ? skip.Error : new RetireInstancesOnStartResponse(0);
        }

        var found = await Scan(cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        var retiring = await RetireOpen(found.Value, grant, cancellationToken).ConfigureAwait(false);
        if (retiring.IsError)
        {
            return retiring.Error;
        }

        var count = retiring.Value;
        var marker = events.Entry(EventTypes.WorkflowRetireDone, new RetirePassDone(count), "workflow:retire-pass:done");
        var marked = await ledger.Append(marker, grant, cancellationToken).ConfigureAwait(false);
        return marked.IsError ? marked.Error : new RetireInstancesOnStartResponse(count);
    }

    // One note per open recipe instance (idempotent by key); returns how many were new.
    private async ValueTask<Result<int>> RetireOpen((Dictionary<string, (string To, string? Item)> Last, HashSet<string> Retired) found, LeaseGrant grant, CancellationToken cancellationToken)
    {
        var (state, retired) = found;
        var count = 0;
        foreach (var (instance, last) in state.Where(s => !retired.Contains(s.Key) && !RetiredRecipeEnds.IsEnd(s.Key, s.Value.To)))
        {
            var entry = events.Entry(EventTypes.WorkflowRetired, new WorkflowEventPayload(instance, "", "retired", TriggerNames.Retire, Outcome: "retired", Run: null), $"workflow:{instance}:retired", last.Item);
            var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
            if (appended.IsError)
            {
                return appended.Error;
            }

            count += appended.Value.AlreadyRecorded ? 0 : 1;
        }

        return count;
    }

    // The pass runs once: the 'workflow.retire.done' marker means an earlier start finished it, and a workflow row written since belongs to the graph engine.
    // A ledger that never held a recipe instance has nothing to retire and gets no marker: one lookup, no page read.
    private async ValueTask<Result<bool>> HasNothingToDo(CancellationToken cancellationToken)
    {
        var done = await ledger.Latest(EventTypes.WorkflowRetireDone, cancellationToken).ConfigureAwait(false);
        if (done.IsError || done.Value is not null)
        {
            return done.IsError ? done.Error : true;
        }

        var anyStarted = await ledger.Latest(EventTypes.WorkflowStarted, cancellationToken).ConfigureAwait(false);
        return anyStarted.IsError ? anyStarted.Error : anyStarted.Value is null;
    }

    /// <summary>Every instance a started event names (with its item) and every instance that already has a retired note.</summary>
    private async ValueTask<Result<(Dictionary<string, (string To, string? Item)> Last, HashSet<string> Retired)>> Scan(CancellationToken cancellationToken)
    {
        var started = new Dictionary<string, (string To, string? Item)>(StringComparer.Ordinal);
        var retired = new HashSet<string>(StringComparer.Ordinal);
        var cursor = 0L;
        while (true)
        {
            var page = await ledger.ReadAfter(cursor, PageSize, cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return page.Error;
            }

            foreach (var recorded in page.Value.Where(static r => Is(r, EventTypes.WorkflowStarted) || Is(r, EventTypes.WorkflowTransition) || Is(r, EventTypes.WorkflowRetired)))
            {
                Fold(recorded, started, retired);
            }

            if (page.Value.Count < PageSize)
            {
                return (started, retired);
            }

            cursor = page.Value[^1].Sequence;
        }
    }

    private void Fold(LedgerEvent recorded, Dictionary<string, (string To, string? Item)> started, HashSet<string> retired)
    {
        var payload = events.Payload<WorkflowEventPayload>(recorded);   // a row that doesn't read is history, not an instance
        if (payload.IsError)
        {
            return;
        }

        var instance = payload.Value.Instance;
        if (Is(recorded, EventTypes.WorkflowRetired))
        {
            _ = retired.Add(instance);
            return;
        }

        if (!IsRecipeAddress(instance))
        {
            return;   // a graph run's rows (its id has no item/recipe/attempt shape) are never retired
        }

        started[instance] = (payload.Value.To, recorded.Item ?? (started.TryGetValue(instance, out var known) ? known.Item : null));
    }

    // The recipe engine's instance address: '{item}/{recipe}/{attempt}', with a child's generation after it. A graph run id ('slug@N') has no '/'.
    private static bool IsRecipeAddress(string instance) =>
        instance.AsSpan().Count('/') >= 2 && !instance.Contains('@', StringComparison.Ordinal);

    private static bool Is(LedgerEvent recorded, string type) => string.Equals(recorded.Envelope.Type, type, StringComparison.Ordinal);
}

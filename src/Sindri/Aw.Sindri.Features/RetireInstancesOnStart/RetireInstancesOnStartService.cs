using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.RetireInstancesOnStart;

/// <summary>
/// Gives every instance the removed recipe engine left behind one 'workflow.retired' note. The ledger is the only source: an instance is
/// every address a 'workflow.started' event names, and one with a note already is skipped (the note's key is per instance, so a restart
/// adds nothing). The ledger doesn't say which recipe states were final, so an instance that had finished also gets the note; it is history only.
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

        var found = await Scan(cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        var (started, retired) = found.Value;
        var count = 0;
        foreach (var (instance, item) in started.Where(s => !retired.Contains(s.Key)))
        {
            var entry = events.Entry(EventTypes.WorkflowRetired, new WorkflowEventPayload(instance, "", "retired", "retire", Outcome: "retired", Run: null), $"workflow:{instance}:retired", item);
            var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
            if (appended.IsError)
            {
                return appended.Error;
            }

            count += appended.Value.AlreadyRecorded ? 0 : 1;
        }

        return new RetireInstancesOnStartResponse(count);
    }

    /// <summary>Every instance a started event names (with its item) and every instance that already has a retired note.</summary>
    private async ValueTask<Result<(Dictionary<string, string?> Started, HashSet<string> Retired)>> Scan(CancellationToken cancellationToken)
    {
        var started = new Dictionary<string, string?>(StringComparer.Ordinal);
        var retired = new HashSet<string>(StringComparer.Ordinal);
        var cursor = 0L;
        while (true)
        {
            var page = await ledger.ReadAfter(cursor, PageSize, cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return page.Error;
            }

            foreach (var recorded in page.Value.Where(static r => Is(r, EventTypes.WorkflowStarted) || Is(r, EventTypes.WorkflowRetired)))
            {
                var payload = events.Payload<WorkflowEventPayload>(recorded);   // a row that doesn't read is history, not an instance
                if (payload.IsSuccess && Is(recorded, EventTypes.WorkflowRetired))
                {
                    _ = retired.Add(payload.Value.Instance);
                }
                else if (payload.IsSuccess)
                {
                    _ = started.TryAdd(payload.Value.Instance, recorded.Item);
                }
            }

            if (page.Value.Count < PageSize)
            {
                return (started, retired);
            }

            cursor = page.Value[^1].Sequence;
        }
    }

    private static bool Is(LedgerEvent recorded, string type) => string.Equals(recorded.Envelope.Type, type, StringComparison.Ordinal);
}

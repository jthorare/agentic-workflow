using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Setup;

/// <summary>
/// Records 'setup.step.changed' (setup-once spec §7.5) for the steps whose derived state differs from the last event of that step: one event per change, fenced by the lease this process holds, under
/// 'setup.step.changed:&lt;id&gt;:&lt;state&gt;:&lt;n&gt;'. The ledger is read once into <see cref="SetupStepLog"/>; after that a tick that finds nothing changed reads nothing.
/// </summary>
/// <param name="held">The lease the events are fenced by.</param>
/// <param name="ledger">Appends the events.</param>
/// <param name="reader">Reads the earlier ones.</param>
/// <param name="events">Builds the entries and reads payloads.</param>
/// <param name="callers">The caller whose command id the events carry.</param>
/// <param name="log">What is known of the earlier events.</param>
public sealed class SetupStepRecorder(HeldLease held, ILedgerStore ledger, ILedgerReader reader, EventFactory events, ICallerContextAccessor callers, SetupStepLog log)
{
    private const int Page = 500;
    private static readonly LedgerFilter _filter = new(TypeFilter.Parse(EventTypes.SetupStepChanged).Value, Item: null);

    /// <summary>Appends the changes of <paramref name="steps"/> against what the ledger holds.</summary>
    /// <param name="steps">The derived steps.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>How many events were appended, or why nothing could be recorded (no lease, no call running, an unreadable ledger).</returns>
    public async ValueTask<Result<int>> Record(IReadOnlyList<SetupStep> steps, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (held.Current is not { } grant || callers.Current is not { } caller)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        if (!log.Loaded && await Load(cancellationToken).ConfigureAwait(false) is { } unread)
        {
            return unread;
        }

        var appended = 0;
        foreach (var change in SetupStepChanges.Plan(steps, log.Snapshot()))
        {
            var payload = new SetupStepChanged(caller.CommandId, SetupStepChanges.Word(change.Id), SetupStepChanges.Word(change.State), change.Code);
            var key = SetupStepChanges.KeyOf(change.Id, change.State, change.Count);
            var result = await ledger.Append(events.Entry(EventTypes.SetupStepChanged, payload, key), grant, cancellationToken).ConfigureAwait(false);
            if (result.IsError)
            {
                return result.Error;
            }

            log.Add(change.Id, change.State, change.Code);
            appended++;
        }

        return appended;
    }

    // Reads every recorded change into the log; the error when the ledger can't be read, so nothing is written on a guess.
    private async ValueTask<Error?> Load(CancellationToken cancellationToken)
    {
        var head = await reader.Head(cancellationToken).ConfigureAwait(false);
        if (head.IsError)
        {
            return head.Error;
        }

        for (var cursor = 0L; cursor < head.Value;)
        {
            var scan = await reader.Scan(cursor, Page, _filter, cancellationToken).ConfigureAwait(false);
            if (scan.IsError)
            {
                return scan.Error;
            }

            foreach (var recorded in scan.Value.Events)
            {
                var payload = events.Payload<SetupStepChanged>(recorded);
                if (payload.IsSuccess && SetupStepChanges.TryStep(payload.Value.Step, out var id) && SetupStepChanges.TryState(payload.Value.State, out var state))
                {
                    log.Add(id, state, payload.Value.Code);
                }
            }

            if (scan.Value.ScannedTo <= cursor)
            {
                break;
            }

            cursor = scan.Value.ScannedTo;
        }

        log.MarkLoaded();
        return null;
    }
}

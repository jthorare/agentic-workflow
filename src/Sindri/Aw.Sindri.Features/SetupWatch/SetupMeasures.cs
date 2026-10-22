using System.Diagnostics.Metrics;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.SetupWatch;

/// <summary>
/// The two measures of setup-once spec §7.5, computed by 'service:setup' from the ledger (no new event): 'setup.done' when a look that appended a change leaves every step done or skipped (the app's first open is the first 'setup.step.changed': the first tick records all six),
/// and 'setup.first-item' on the first 'workflow.started' after it. Each is measured once per process (<see cref="SetupMeasureLog"/>); a process that starts on a finished setup measures neither.
/// </summary>
/// <param name="reader">Reads the setup changes and the workflow starts.</param>
/// <param name="log">What this process has measured.</param>
internal sealed class SetupMeasures(ILedgerReader reader, SetupMeasureLog log)
{
    private const int Page = 500;
    private static readonly Meter _meter = new("Aw.Sindri");
    private static readonly Histogram<double> _done = _meter.CreateHistogram<double>("sindri.setup.done", unit: "min", description: "From the app's first open to every setup step done or skipped");
    private static readonly Histogram<double> _firstItem = _meter.CreateHistogram<double>("sindri.setup.first_item", unit: "min", description: "From setup done to the first work item's first start");

    /// <summary>Looks for what became measurable and records the metrics.</summary>
    /// <param name="steps">The derived steps of this look.</param>
    /// <param name="appended">How many changes this look recorded.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>What was measured now; an unreadable ledger measures nothing and the next look tries again.</returns>
    internal async ValueTask<SetupMeasured> Observe(IReadOnlyList<SetupStep> steps, int appended, CancellationToken cancellationToken)
    {
        TimeSpan? done = null;
        TimeSpan? firstItem = null;
        if (log.Done is null && appended > 0 && steps.All(static s => s.State is SetupState.Done or SetupState.Skipped))
        {
            done = await MeasureDone(cancellationToken).ConfigureAwait(false);
        }

        if (log.Done is { } point && !log.FirstItemMeasured)
        {
            firstItem = await MeasureFirstItem(point.At, cancellationToken).ConfigureAwait(false);
        }

        if (done is { } d)
        {
            _done.Record(d.TotalMinutes);
        }

        if (firstItem is { } f)
        {
            _firstItem.Record(f.TotalMinutes);
        }

        return new SetupMeasured(done, firstItem);
    }

    private async ValueTask<TimeSpan?> MeasureDone(CancellationToken cancellationToken)
    {
        LedgerEvent? first = null;
        LedgerEvent? last = null;
        await foreach (var recorded in Scan(EventTypes.SetupStepChanged, 0, cancellationToken).ConfigureAwait(false))
        {
            first ??= recorded;
            last = recorded;
        }

        if (first is null || last is null)
        {
            return null;
        }

        log.MarkDone(last.Envelope.OccurredAt, last.Sequence);
        return last.Envelope.OccurredAt - first.Envelope.OccurredAt;
    }

    private async ValueTask<TimeSpan?> MeasureFirstItem(DateTimeOffset doneAt, CancellationToken cancellationToken)
    {
        var head = await reader.Head(cancellationToken).ConfigureAwait(false);   // read before the scan: a start that lands during it is found next time
        await foreach (var recorded in Scan(EventTypes.WorkflowStarted, log.ItemCursor, cancellationToken).ConfigureAwait(false))
        {
            log.MarkFirstItem();
            return recorded.Envelope.OccurredAt - doneAt;
        }

        if (head.IsSuccess)
        {
            log.AdvanceCursor(head.Value);
        }

        return null;
    }

    // The events of one type after a sequence, in order; an unreadable ledger ends the scan.
    private async IAsyncEnumerable<LedgerEvent> Scan(string type, long after, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var filter = new LedgerFilter(TypeFilter.Parse(type).Value, Item: null);
        var head = await reader.Head(cancellationToken).ConfigureAwait(false);
        if (head.IsError)
        {
            yield break;
        }

        for (var cursor = after; cursor < head.Value;)
        {
            var scan = await reader.Scan(cursor, Page, filter, cancellationToken).ConfigureAwait(false);
            if (scan.IsError)
            {
                yield break;
            }

            foreach (var recorded in scan.Value.Events)
            {
                yield return recorded;
            }

            if (scan.Value.ScannedTo <= cursor)
            {
                yield break;
            }

            cursor = scan.Value.ScannedTo;
        }
    }
}

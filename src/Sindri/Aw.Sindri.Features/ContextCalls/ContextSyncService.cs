using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The 'context-sync' scheduled service (context spec §8.8, S-5B): syncs the intent sources into the intent layer on the profile's cadence and records
/// one 'context.sources.synced' event per source. It runs outside the command gate, like 'context-rebuild', which is how the stage works before API P6's job route.
/// A source that is down is a row with its code (the event carries it); a sync of the same source already running ends this run quietly.
/// </summary>
internal sealed class ContextSyncService(ContextScopes scopes, IContextEngine engine, ILedgerStore ledger, EventFactory events) : IScheduledService
{
    internal static ServiceName ServiceId { get; } = ServiceName.From("context-sync");

    public ServiceName Identity() => ServiceId;

    public async ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError || scope.Value.Settings is null)
        {
            return scope.IsError ? scope.Error : ScheduledResult.Quiet;
        }

        var view = await engine.Sources(scope.Value, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            return view.Error;
        }

        var worked = false;
        foreach (var source in view.Value.Sources.Where(r => r.Kind == SourceKind.Intent).Select(r => r.Source).Distinct())
        {
            var done = await SyncOne(source, scope.Value, run, cancellationToken).ConfigureAwait(false);
            if (done.IsError)
            {
                return done.Error;
            }

            worked |= done.Value;
        }

        return worked ? ScheduledResult.Worked : ScheduledResult.Quiet;
    }

    /// <summary>Syncs one source and records its event; false when a sync of it was running already (that one records its own).</summary>
    private async ValueTask<Result<bool>> SyncOne(SourceId source, ContextScope scope, ScheduledRun run, CancellationToken cancellationToken)
    {
        var report = await engine.Sync(source.Value, scope, cancellationToken).ConfigureAwait(false);
        if (report.IsError)
        {
            return report.Error.Code == ContextErrors.SyncRunning.Code ? false : report.Error;
        }

        foreach (var row in report.Value.Sources)
        {
            var recorded = await Record(row, run, cancellationToken).ConfigureAwait(false);
            if (recorded.IsError)
            {
                return recorded.Error;
            }
        }

        return report.Value.Sources.Count > 0;
    }

    private async ValueTask<Result<Unit>> Record(SourceSync row, ScheduledRun run, CancellationToken cancellationToken)
    {
        var payload = new ContextSourcesSynced(row.Source.Value, run.Slot, [],
            new ContextLinksSynced(row.Records, row.Fetched, row.Unchanged, row.Linked, row.Unlinked, row.Orphaned, row.Decisions, row.Queued, row.Refused, row.Tokens, row.Code));
        var key = string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ContextSourcesSynced}:{run.Grant.Epoch}:{row.Source.Value}:{run.Slot:O}");
        var recorded = await ledger.Append(events.Entry(EventTypes.ContextSourcesSynced, payload, key), run.Grant, cancellationToken).ConfigureAwait(false);
        return recorded.IsError ? recorded.Error : Unit.Value;
    }
}

using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The work of a sync job (context spec §8.8; API P6): syncs the intent sources through the engine under the starting caller, which is the same work as
/// the 'context-sync' service does: one source at a time, recording 'context.sources.synced' for each as it finishes (slot: when the job began). A source
/// that is down is a row with its code. A source that is running already (the scheduled service's, SND-CONTEXT-056) is a row with that code in a sync of every
/// source, and fails the job when it is the one named.
/// </summary>
/// <param name="scopes">The profile's scope and the caller.</param>
/// <param name="engine">The context engine.</param>
/// <param name="held">The lease the events are fenced by.</param>
/// <param name="ledger">Appends the events.</param>
/// <param name="events">Builds the entries.</param>
/// <param name="jobs">The running job: its progress takes a line.</param>
/// <param name="clock">Dates the slot.</param>
internal sealed class SyncContextSourcesService(ContextScopes scopes, IContextEngine engine, HeldLease held, ILedgerStore ledger, EventFactory events, IJobContextAccessor jobs, TimeProvider clock)
    : IService<SyncContextSourcesRequest, SyncContextSourcesResult>
{
    public async ValueTask<Result<SyncContextSourcesResult>> Handle(SyncContextSourcesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var slot = clock.GetUtcNow();
        var wanted = await Wanted(request.Source, scope.Value, cancellationToken).ConfigureAwait(false);
        if (wanted.IsError)
        {
            return wanted.Error;
        }

        var synced = new List<SyncedSource>();
        foreach (var source in wanted.Value)
        {
            jobs.Current?.Progress.Report($"syncing {source}");
            var done = await SyncOne(source, request.Source is null, (scope.Value, slot, grant), cancellationToken).ConfigureAwait(false);
            if (done.IsError)
            {
                return done.Error;   // the sources before this one have their events already
            }

            synced.AddRange(done.Value);
        }

        return new SyncContextSourcesResult(synced);
    }

    /// <summary>The sources to sync one at a time, as the 'context-sync' service does: the one named, or every intent source.</summary>
    private async ValueTask<Result<IReadOnlyList<string>>> Wanted(string? named, ContextScope scope, CancellationToken cancellationToken)
    {
        if (named is not null)
        {
            return new[] { named };
        }

        var view = await engine.Sources(scope, cancellationToken).ConfigureAwait(false);
        return view.IsError ? view.Error : Results.Success<IReadOnlyList<string>>([.. view.Value.Sources.Where(static r => r.Kind == SourceKind.Intent).Select(static r => r.Source.Value).Distinct(StringComparer.Ordinal)]);
    }

    /// <summary>
    /// Syncs one source and records its event. In a sync of every source, one that is running already (056) is its own row with the code and the others go on; a
    /// named source in that state fails the job.
    /// </summary>
    private async ValueTask<Result<IReadOnlyList<SyncedSource>>> SyncOne(string source, bool all, (ContextScope Scope, DateTimeOffset Slot, LeaseGrant Grant) run, CancellationToken cancellationToken)
    {
        var report = await engine.Sync(source, run.Scope, cancellationToken).ConfigureAwait(false);
        if (report.IsError)
        {
            return all && report.Error.Code == ContextErrors.SyncRunning.Code
                ? Results.Success<IReadOnlyList<SyncedSource>>([new SyncedSource(source, new ContextLinksSynced(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, report.Error.Code))])
                : report.Error;
        }

        foreach (var row in report.Value.Sources)
        {
            // the engine has applied the sync: its events are appended even when the job is being cancelled
            var recorded = await Record(row, run.Slot, run.Grant, CancellationToken.None).ConfigureAwait(false);
            if (recorded.IsError)
            {
                return recorded.Error;
            }
        }

        return Results.Success<IReadOnlyList<SyncedSource>>([.. report.Value.Sources.Select(static r => new SyncedSource(r.Source.Value, Links(r)))]);
    }

    private static ContextLinksSynced Links(SourceSync row) =>
        new(row.Records, row.Fetched, row.Unchanged, row.Linked, row.Unlinked, row.Orphaned, row.Decisions, row.Queued, row.Refused, row.Tokens, row.Code);

    private async ValueTask<Result<Unit>> Record(SourceSync row, DateTimeOffset slot, LeaseGrant grant, CancellationToken cancellationToken)
    {
        var payload = new ContextSourcesSynced(row.Source.Value, slot, [], Links(row));
        var key = string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ContextSourcesSynced}:{grant.Epoch}:{row.Source.Value}:{slot:O}");
        var appended = await ledger.Append(events.Entry(EventTypes.ContextSourcesSynced, payload, key), grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : Unit.Value;
    }
}

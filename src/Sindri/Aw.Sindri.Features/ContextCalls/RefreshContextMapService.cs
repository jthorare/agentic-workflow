using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Context.Core.Review;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The work of a refresh job (owner F1, 2026-10-19): reads the mapped repos' heads, builds the draft, lets the cleanup agent settle its review queue,
/// promotes through the engine (not the protected command: the owner decided the service promotes, as the job's caller) and records 'context.map.built',
/// 'context.map.promoted' and 'context.map.refreshed'. Any step's error fails the job with the later steps not run; a built draft stays.
/// </summary>
/// <param name="scopes">The profile's scope and the caller.</param>
/// <param name="engine">The context engine.</param>
/// <param name="held">The lease the events are fenced by.</param>
/// <param name="ledger">Appends the events.</param>
/// <param name="events">Builds the entries.</param>
/// <param name="jobs">The running job: its progress takes the step lines.</param>
/// <param name="clock">Dates the build's slot.</param>
internal sealed class RefreshContextMapService(ContextScopes scopes, IContextEngine engine, HeldLease held, ILedgerStore ledger, EventFactory events, IJobContextAccessor jobs, TimeProvider clock)
    : IService<RefreshContextMapRequest, RefreshContextMapResult>
{
    public async ValueTask<Result<RefreshContextMapResult>> Handle(RefreshContextMapRequest request, CancellationToken cancellationToken)
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

        var progress = jobs.Current?.Progress;
        var heads = await engine.MapHeads(scope.Value, cancellationToken).ConfigureAwait(false);
        if (heads.IsError)
        {
            return heads.Error;
        }

        var recorder = new ContextMapEvents(ledger, events, grant);
        var built = await Build(recorder, progress, scope.Value, cancellationToken).ConfigureAwait(false);
        if (built.IsError)
        {
            return built.Error;
        }

        var settled = await CleanAndPromote(recorder, progress, scope.Value, cancellationToken).ConfigureAwait(false);
        if (settled.IsError)
        {
            return settled.Error;
        }

        var (version, report) = settled.Value;
        var result = new RefreshContextMapResult(version.Repo.Value, version.Number,
            heads.Value.ToDictionary(static h => h.Repo.Value, static h => h.Head, StringComparer.Ordinal),
            report.Accepted, report.Removed, report.Dropped, Mode(report.Mode));
        var refreshed = await recorder.Refreshed(result, cancellationToken).ConfigureAwait(false);
        return refreshed.IsError ? refreshed.Error : result;
    }

    private async ValueTask<Result<Unit>> Build(ContextMapEvents recorder, IProgress<string>? progress, ContextScope scope, CancellationToken cancellationToken)
    {
        var slot = clock.GetUtcNow();
        progress?.Report("building");
        var built = await engine.BuildMap(progress, scope, cancellationToken).ConfigureAwait(false);
        if (built.IsError)
        {
            return built.Error;
        }

        var recorded = await recorder.Built(built.Value, slot, cancellationToken).ConfigureAwait(false);
        return recorded.IsError ? recorded.Error : Unit.Value;
    }

    private async ValueTask<Result<(MapVersion Version, CleanupReport Report)>> CleanAndPromote(ContextMapEvents recorder, IProgress<string>? progress, ContextScope scope, CancellationToken cancellationToken)
    {
        var queue = await engine.ReviewQueue(limit: null, scope, cancellationToken).ConfigureAwait(false);
        if (queue.IsError)
        {
            return queue.Error;
        }

        progress?.Report(string.Create(CultureInfo.InvariantCulture, $"cleaning up: {queue.Value.Undecided} to decide"));
        var cleaned = await engine.CleanDraft(scope, cancellationToken).ConfigureAwait(false);
        if (cleaned.IsError)
        {
            return cleaned.Error;
        }

        progress?.Report("promoting");
        var promoted = await engine.Promote(scope, cancellationToken).ConfigureAwait(false);
        if (promoted.IsError)
        {
            return promoted.Error;
        }

        var appended = await recorder.Promoted(promoted.Value, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : (promoted.Value.Version, cleaned.Value);
    }

    private static string Mode(CleanupMode mode) => mode switch
    {
        CleanupMode.None => "none",
        CleanupMode.Agent => "agent",
        CleanupMode.Partial => "partial",
        CleanupMode.Unavailable => "unavailable",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, message: null),
    };
}

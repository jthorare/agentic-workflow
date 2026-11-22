using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Home;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Features.HomeCollect;

namespace Aw.Sindri.Features.HomeRuns;

/// <summary>
/// The 'home-runs' collector (home spec §3.2 'run-failed', §3.3, E1): each minute it raises a Home row for every live run that ended failed and that the owner
/// has not seen, and clears the row of a run that is seen now. A row the owner marked done or muted marks its run seen first, so the row clears and the run
/// never comes back (a clear ends the episode, spec §5.4).
/// </summary>
/// <param name="runs">The run store: the failed runs nobody has seen (Home spec §3.3), and the seen mark.</param>
/// <param name="view">The Home rows this collector raised before.</param>
/// <param name="writer">Appends the pass's events.</param>
/// <param name="scrubber">Scrubs the run's error before it reaches a row.</param>
/// <param name="clock">The read time each row's source carries.</param>
internal sealed class HomeRunsService(IWorkflowRunStore runs, IHomeViewReader view, HomeRowWriter writer, ITextScrubber scrubber, TimeProvider clock) : IScheduledService
{
    /// <summary>The most failed runs one pass raises rows for (the newest first).</summary>
    internal const int FailedLimit = 200;

    /// <summary>What the service does, as GET /sindri/services says it.</summary>
    public const string Description = "Puts each failed workflow run the owner has not seen on Home, and takes it off once it is seen.";

    private static readonly HashSet<HomeKind> _kinds = [HomeKind.RunFailed];

    internal static ServiceName ServiceId { get; } = ServiceName.From(HomeRunRows.SourceName);

    /// <summary>Runs without a profile entry, every minute.</summary>
    internal static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(TimeSpan.FromMinutes(1)), Description);

    public ServiceName Identity() => ServiceId;

    public async ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var failed = await runs.FailedUnseen(cancellationToken).ConfigureAwait(false);
        if (failed.IsError)
        {
            return failed.Error;
        }

        var held = await HomeHeldRows.Of(view, HomeRunRows.SourceName, _kinds, cancellationToken).ConfigureAwait(false);
        if (held.IsError)
        {
            return held.Error;
        }

        var putAway = await MarkPutAwaySeen(held.Value, cancellationToken).ConfigureAwait(false);
        if (putAway.IsError)
        {
            return putAway.Error;
        }

        var now = clock.GetUtcNow();
        var wanted = failed.Value.Take(FailedLimit).Select(r => HomeRunRows.Failed(r, now, scrubber)).Where(r => !putAway.Value.Contains(r.Row.Id.Value)).ToList();
        var plan = HomeReconcile.Plan(wanted, held.Value);
        if (plan.IsEmpty)
        {
            return ScheduledResult.Quiet;
        }

        var written = await writer.Write(plan, run, cancellationToken).ConfigureAwait(false);
        return written.IsError
            ? written.Error
            : new ScheduledResult(Idle: false, string.Create(CultureInfo.InvariantCulture, $"ok: {plan.Raised.Count} failed run(s) raised, {plan.Cleared.Count} cleared"));
    }

    // The rows the owner put away (done or muted): each one's run is marked seen. Returns their row ids.
    private async ValueTask<Result<IReadOnlySet<string>>> MarkPutAwaySeen(IReadOnlyList<HomeRow> held, CancellationToken cancellationToken)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in held.Where(static r => r.State is HomeState.Done or HomeState.Muted))
        {
            if (HomeRunRows.RunOf(row.Id) is { } failedRun)
            {
                var marked = await runs.MarkSeen(failedRun, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
                if (marked.IsError && marked.Error.Code != SindriErrors.GraphRunNotFound.Code)   // why: a run purged meanwhile has nothing to mark; its row still clears
                {
                    return marked.Error;
                }
            }

            _ = ids.Add(row.Id.Value);
        }

        return Results.Success<IReadOnlySet<string>>(ids);
    }
}

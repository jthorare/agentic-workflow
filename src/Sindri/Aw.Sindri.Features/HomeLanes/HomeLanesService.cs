using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lanes;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.HomeLanes;

/// <summary>
/// The 'home-lanes' background service (Home spec §3.3, E1), every 30 seconds: runs 'aw-meta status', 'lanes-context' and 'usage' read-only through <see cref="ILaneSource"/>,
/// opens the lane-recycle, agent-limited and pr-ready-merge rows whose trigger holds and clears the open ones whose trigger stopped. When aw-meta does not answer it changes no row
/// and says 'lanes-unavailable' in its run summary, which 'sindri services' shows.
/// </summary>
/// <param name="source">Reads aw-meta.</param>
/// <param name="rows">Opens and clears the rows.</param>
internal sealed class HomeLanesService(ILaneSource source, ILaneRows rows) : IScheduledService
{
    internal static ServiceName ServiceId { get; } = ServiceName.From("home-lanes");

    /// <summary>What the service does, as GET /sindri/services says it.</summary>
    public const string Description = "Reads aw-meta for lanes at their recycle mark, limited agents and queued PRs, and puts them on Home.";

    /// <summary>Runs without a profile entry, every 30 seconds.</summary>
    internal static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(TimeSpan.FromSeconds(30)), Description);

    public ServiceName Identity() => ServiceId;

    public async ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        var snapshot = await Read(cancellationToken).ConfigureAwait(false);
        if (snapshot.IsError)
        {
            return new ScheduledResult(Idle: true, "lanes-unavailable: " + snapshot.Error.Message);   // rows stay as they are: an unanswered aw-meta says nothing about them
        }

        var open = await rows.Open(cancellationToken).ConfigureAwait(false);
        if (open.IsError)
        {
            return open.Error;
        }

        var wanted = LaneTriggers.Rows(snapshot.Value);
        var wantedIds = wanted.Select(static r => LaneTriggers.Id(r.Kind, r.Key)).ToHashSet();
        var raised = 0;
        foreach (var row in wanted.Where(r => !open.Value.Contains(LaneTriggers.Id(r.Kind, r.Key))))
        {
            var done = await rows.Raise(row.Kind, row.Key, row.Title, row.Reason, cancellationToken).ConfigureAwait(false);
            if (done.IsError)
            {
                return done.Error;
            }

            raised++;
        }

        var cleared = 0;
        foreach (var id in open.Value.Where(id => !wantedIds.Contains(id)))
        {
            var done = await rows.Clear(id, cancellationToken).ConfigureAwait(false);
            if (done.IsError)
            {
                return done.Error;
            }

            cleared++;
        }

        return raised + cleared == 0 ? ScheduledResult.Quiet : new ScheduledResult(Idle: false, string.Create(CultureInfo.InvariantCulture, $"opened {raised}, cleared {cleared}"));
    }

    private async ValueTask<Result<LaneSnapshot>> Read(CancellationToken cancellationToken)
    {
        var status = await source.Status(cancellationToken).ConfigureAwait(false);
        var context = status.IsError ? status : await source.Context(cancellationToken).ConfigureAwait(false);
        var usage = context.IsError ? context : await source.Usage(cancellationToken).ConfigureAwait(false);
        return usage.IsError ? usage.Error : LaneParser.Parse(status.Value, context.Value, usage.Value);
    }
}

using System.Diagnostics.CodeAnalysis;
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
[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "Red stub of #3733: only the tests use it until the fix commit registers it.")]
internal sealed class HomeLanesService(ILaneSource source, ILaneRows rows) : IScheduledService
{
    internal static ServiceName ServiceId { get; } = ServiceName.From("home-lanes");

    /// <summary>What the service does, as GET /sindri/services says it.</summary>
    public const string Description = "Reads aw-meta for lanes at their recycle mark, limited agents and queued PRs, and puts them on Home.";

    /// <summary>Runs without a profile entry, every 30 seconds.</summary>
    internal static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(TimeSpan.FromSeconds(30)), Description);

    public ServiceName Identity() => ServiceId;

    public ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        _ = (source, rows);   // red stub: the real run reads aw-meta and diffs the rows
        throw new NotSupportedException();
    }
}

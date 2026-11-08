using Aw.Kernel;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.WorkflowSchedule;

// why: the fix commit registers it; until then only tests build it.
#pragma warning disable MA0182

/// <summary>
/// The 'workflow-schedule' background service (issue 2258, PR 6): each minute it starts one run for the latest tick a workflow's
/// 'schedule' node has passed and not yet fired. Several missed ticks (a laptop asleep for days) fire only the latest.
/// </summary>
/// <param name="workflows">The workflows and their revisions.</param>
/// <param name="runs">The runs, which say whether a tick already fired.</param>
/// <param name="starter">Starts, queues or skips the run.</param>
/// <param name="clock">The time a tick is measured against.</param>
internal sealed class WorkflowScheduleService(IWorkflowStore workflows, IWorkflowRunStore runs, IGraphRunStarter starter, TimeProvider clock) : IScheduledService
{
    internal static ServiceName ServiceId { get; } = ServiceName.From("workflow-schedule");

    /// <summary>What the service does, as GET /sindri/services says it.</summary>
    public const string Description = "Starts a run of each active workflow whose schedule has a tick to fire, once for the latest missed tick.";

    /// <summary>Runs without a profile entry, every minute, so a fresh profile still runs schedules.</summary>
    internal static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(TimeSpan.FromMinutes(1)), Description);

    public ServiceName Identity() => ServiceId;

    public ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        _ = (workflows, runs, starter, clock);
        throw new NotSupportedException();
    }
}
#pragma warning restore MA0182

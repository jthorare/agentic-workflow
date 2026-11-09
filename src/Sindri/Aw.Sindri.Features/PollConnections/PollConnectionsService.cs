using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Features.PollConnections;

/// <summary>The 'workflow-poll' background service (#2258, PR 11): polls connections for the events that connection_event triggers watch.</summary>
internal sealed class PollConnectionsService : IScheduledService
{
    internal PollConnectionsService(IWorkflowStore workflows, IConnectionStore connections, IEnumerable<IEventPoller> pollers, IPollCursors cursors, CatchUp read, PollWake wake, IApprovedProfile approved, TimeProvider clock, ILogger<PollConnectionsService>? logger = null) =>
        _ = (workflows, connections, pollers, cursors, read, wake, approved, clock, logger);

    internal static ServiceName ServiceId { get; } = ServiceName.From("workflow-poll");

    /// <summary>What the service does, as GET /sindri/services says it.</summary>
    public const string Description = "Polls connections for events that start workflows (connection_event triggers), and reads missed events after a start or a wake.";

    /// <summary>Runs without a profile entry, every 30 seconds.</summary>
    internal static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(TimeSpan.FromSeconds(30)), Description);

    public ServiceName Identity() => ServiceId;

    public ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken) => throw new NotSupportedException("unbuilt: " + run + cancellationToken);
}

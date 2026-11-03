using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The 'context-upkeep' background service (#1953): every 15 minutes while serve runs it refreshes context.db's planner statistics and checkpoints its write-ahead log,
/// through <see cref="IContextEngine"/> only and without a profile (the store is maintained either way). A reader or writer holding the log makes the run 'partial:', never a wait.
/// </summary>
/// <param name="engine">Context's facade: its upkeep member is Upkeep.</param>
internal sealed class ContextUpkeepService(IContextEngine engine) : IScheduledService
{
    internal static ServiceName ServiceId { get; } = ServiceName.From("context-upkeep");

    /// <summary>What the service does, as GET /sindri/services says it.</summary>
    public const string Description = "Checkpoints context.db's log and refreshes its query stats.";

    /// <summary>Runs without a profile entry, every 15 minutes.</summary>
    internal static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(TimeSpan.FromMinutes(15)), Description);

    public ServiceName Identity() => ServiceId;

    public ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        _ = engine;
        throw new NotSupportedException();
    }
}

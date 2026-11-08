using System.Globalization;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
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
/// <param name="stores">The ledger's and views.db's log checkpoint (#2221), run on the same tick; a store a reader holds shows in the summary as 'partial:', never as an error.</param>
internal sealed class ContextUpkeepService(IContextEngine engine, IStoreCheckpoints stores) : IScheduledService
{
    internal static ServiceName ServiceId { get; } = ServiceName.From("context-upkeep");

    /// <summary>What the service does, as GET /sindri/services says it.</summary>
    public const string Description = "Checkpoints context.db's log and refreshes its query stats.";

    /// <summary>Runs without a profile entry, every 15 minutes.</summary>
    internal static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(TimeSpan.FromMinutes(15)), Description);

    public ServiceName Identity() => ServiceId;

    public async ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var upkeep = await engine.Upkeep(cancellationToken).ConfigureAwait(false);
        var checkpoints = await stores.Run(cancellationToken).ConfigureAwait(false);
        if (upkeep.IsError)
        {
            return upkeep.Error;
        }

        var context = Summarize(upkeep.Value);
        var held = checkpoints.Where(static c => !c.Truncated).Select(static c => c.Store).ToList();
        if (held.Count == 0)
        {
            return context;
        }

        var storeNote = $"{string.Join(", ", held)} held by a reader, log not truncated";
        return new ScheduledResult(Idle: false, context.Idle ? $"partial: {storeNote}" : $"{context.Summary}; {storeNote}");
    }

    /// <summary>The run's outcome: quiet with no store or nothing in the log, 'ok:' once the log is truncated, 'partial:' while a reader or writer holds it.</summary>
    internal static ScheduledResult Summarize(StoreUpkeep? upkeep)
    {
        if (upkeep is null || (upkeep.Truncated && upkeep.LogPages == 0))
        {
            return ScheduledResult.Quiet;
        }

        return new ScheduledResult(Idle: false, upkeep.Truncated
            ? string.Create(CultureInfo.InvariantCulture, $"ok: log file truncated (it held {upkeep.LogPages} pages)")
            : string.Create(CultureInfo.InvariantCulture, $"partial: a reader or writer held the log ({upkeep.CheckpointedPages} of the log file's {upkeep.LogPages} pages checkpointed)"));
    }
}

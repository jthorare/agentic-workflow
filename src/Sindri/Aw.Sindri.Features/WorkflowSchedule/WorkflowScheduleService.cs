using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Graph.Schedule;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.WorkflowSchedule;

// why: registered, with its BuiltIn entry, in the GraphWorkflow piece together with the launcher; until then only tests build it.
#pragma warning disable MA0182
/// <summary>
/// The 'workflow-schedule' background service (issue 2258, PR 6): each minute it starts one run for the latest tick a workflow's
/// 'schedule' node has passed and not yet fired. Several missed ticks (a laptop asleep for days) fire only the latest. The last
/// fired tick is read from the workflow's newest schedule run (its occurrence is the tick in whole minutes since the Unix epoch),
/// so no state of its own is kept; the starter refuses a tick that already has a run (SND-GRAPH-100).
/// </summary>
/// <param name="workflows">The workflows and their revisions.</param>
/// <param name="runs">The runs, which say whether a tick already fired.</param>
/// <param name="starter">Starts, queues or skips the run.</param>
/// <param name="clock">The time a tick is measured against.</param>
internal sealed class WorkflowScheduleService(IWorkflowStore workflows, IWorkflowRunStore runs, IGraphRunStarter starter, TimeProvider clock) : IScheduledService
{
    internal const string TriggerKind = "schedule";

    internal static ServiceName ServiceId { get; } = ServiceName.From("workflow-schedule");

    /// <summary>What the service does, as GET /sindri/services says it.</summary>
    public const string Description = "Starts a run of each active workflow whose schedule has a tick to fire, once for the latest missed tick.";

    /// <summary>Runs without a profile entry, every minute, so a fresh profile still runs schedules.</summary>
    internal static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(TimeSpan.FromMinutes(1)), Description);

    public ServiceName Identity() => ServiceId;

    public async ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var listed = await workflows.List(includeArchived: false, cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        var now = clock.GetUtcNow();
        var started = 0;
        foreach (var record in listed.Value.Where(static r => r.ActiveRevision is not null))
        {
            var fired = await Fire(record, now, cancellationToken).ConfigureAwait(false);
            if (fired.IsError)
            {
                return fired.Error;
            }

            started += fired.Value ? 1 : 0;
        }

        return started == 0 ? ScheduledResult.Quiet : new ScheduledResult(Idle: false, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"ok: started {started} scheduled run(s)"));
    }

    private async ValueTask<Result<bool>> Fire(WorkflowRecord record, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var revision = await workflows.GetRevision(record.Id, record.ActiveRevision, cancellationToken).ConfigureAwait(false);
        if (revision.IsError)
        {
            return revision.Error;
        }

        var node = revision.Value.Spec.Nodes.FirstOrDefault(static n => string.Equals(n.Type, TriggerKind, StringComparison.Ordinal));
        if (node is null)
        {
            return false;
        }

        var last = await runs.LatestOccurrence(record.Id, TriggerKind, cancellationToken).ConfigureAwait(false);
        if (last.IsError)
        {
            return last.Error;
        }

        // why: no activation time is stored without a ledger migration, so the revision's save time is the floor; a tick between saving and activating can fire once on activation.
        var after = revision.Value.CreatedAt;
        if (last.Value is { } minutes && DateTimeOffset.UnixEpoch.AddMinutes(minutes) is var lastTick && lastTick > after)
        {
            after = lastTick;
        }

        var tick = LatestTick(node, after, now);
        return tick is null ? false : await Start(record, revision.Value, tick.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<bool>> Start(WorkflowRecord record, WorkflowRevision revision, DateTimeOffset tick, CancellationToken cancellationToken)
    {
        var trigger = new JsonObject { ["kind"] = TriggerKind, ["scheduledFor"] = tick.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture) };
        var occurrence = (int)(tick.ToUnixTimeSeconds() / 60);
        var request = new GraphStartRequest(record.Id, record.Slug, revision.Revision, occurrence, trigger.ToJsonString(), Concurrency(revision.Spec.Concurrency));
        var started = await starter.Start(request, cancellationToken).ConfigureAwait(false);
        // why: a tick that already has a run is done, not a failure.
        return started.IsError ? started.Error.Code == SindriErrors.GraphRunIdTaken.Code ? false : started.Error : started.Value.Kind != GraphStartKind.Skipped;
    }

    private static GraphConcurrency Concurrency(string? mode) => mode switch
    {
        "queue" => GraphConcurrency.Queue,
        "unlimited" => GraphConcurrency.Unlimited,
        _ => GraphConcurrency.OnePerWorkflow,
    };

    private static DateTimeOffset? LatestTick(GraphNode node, DateTimeOffset after, DateTimeOffset now)
    {
        var timezone = node.Config["timezone"]?.GetValue<string>();
        DateTimeOffset? latest = null;
        foreach (var text in Crons(node.Config["cron"]))
        {
            if (CronExpression.TryParse(text, timezone, out var cron, out _) && cron.LastTick(after, now) is { } tick && (latest is null || tick > latest))
            {
                latest = tick;
            }
        }

        return latest;
    }

    private static IEnumerable<string> Crons(JsonNode? node) => node switch
    {
        JsonValue v when v.TryGetValue<string>(out var text) => [text],
        JsonArray list => list.OfType<JsonValue>().Select(static v => v.TryGetValue<string>(out var s) ? s : null).OfType<string>(),
        _ => [],
    };
}
#pragma warning restore MA0182

using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Graph.Schedule;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aw.Sindri.Features.WorkflowSchedule;

/// <summary>
/// The 'workflow-schedule' background service (issue 2258, PR 6): each minute it starts one run for the latest tick a workflow's
/// 'schedule' node has passed and not yet handled. Several missed ticks (a laptop asleep for days) fire only the latest. A tick
/// counts as handled once the starter started it, queued it or skipped it (a run was in flight): the store records it
/// (<see cref="IWorkflowRunStore.MarkTick"/>), so a skipped tick is done and never fires late when the run before it ends. A tick whose
/// launch failed is not recorded and is tried again on the next pass. The newest schedule run's occurrence (the tick in whole minutes
/// since the Unix epoch) is a second floor, and the starter refuses a tick that already has a run (SND-GRAPH-100).
/// </summary>
/// <param name="workflows">The workflows and their revisions.</param>
/// <param name="runs">The runs, which say whether a tick already fired.</param>
/// <param name="starter">Starts, queues or skips the run.</param>
/// <param name="clock">The time a tick is measured against.</param>
/// <param name="logger">Where a launch that failed and will be tried again is logged.</param>
internal sealed partial class WorkflowScheduleService(IWorkflowStore workflows, IWorkflowRunStore runs, IGraphRunStarter starter, TimeProvider clock, ILogger<WorkflowScheduleService>? logger = null) : IScheduledService
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
        Error? first = null;
        foreach (var record in listed.Value.Where(static r => r.ActiveRevision is not null))
        {
            var fired = await Fire(record, now, cancellationToken).ConfigureAwait(false);
            if (fired.IsError)
            {
                first ??= fired.Error;   // why: one workflow's error must not keep the others from firing; the first is returned at the end
                continue;
            }

            started += fired.Value ? 1 : 0;
        }

        if (first is { } failure)
        {
            return failure;
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

        var floor = await Floor(record.Id, revision.Value, cancellationToken).ConfigureAwait(false);
        if (floor.IsError)
        {
            return floor.Error;
        }

        var tick = LatestTick(node, floor.Value, now);
        return tick is null ? false : await Start(record, revision.Value, tick.Value, ScheduleStamp.ZoneOf(node), cancellationToken).ConfigureAwait(false);
    }

    // why: ticks at or before this are handled: the revision's save time (no activation time is stored without a ledger migration, so a tick between saving and
    // activating can fire once on activation), the newest schedule run's tick, and the last tick the store recorded (a skipped or queued one has no run row).
    private async ValueTask<Result<DateTimeOffset>> Floor(WorkflowId workflow, WorkflowRevision revision, CancellationToken cancellationToken)
    {
        var last = await runs.LatestOccurrence(workflow, TriggerKind, cancellationToken).ConfigureAwait(false);
        var marked = await runs.LastTick(workflow, cancellationToken).ConfigureAwait(false);
        if (last.IsError || marked.IsError)
        {
            return last.IsError ? last.Error : marked.Error;
        }

        var floor = revision.CreatedAt;
        if (last.Value is { } minutes && DateTimeOffset.UnixEpoch.AddMinutes(minutes) is var lastTick && lastTick > floor)
        {
            floor = lastTick;
        }

        return marked.Value is { } handled && handled > floor ? handled : floor;
    }

    private async ValueTask<Result<bool>> Start(WorkflowRecord record, WorkflowRevision revision, DateTimeOffset tick, string? zone, CancellationToken cancellationToken)
    {
        var trigger = new JsonObject { ["kind"] = TriggerKind };
        ScheduleStamp.Add(trigger, tick, zone);
        var occurrence = checked((int)(tick.ToUnixTimeSeconds() / 60));   // why: minutes since the epoch fit an int until the year 6000; checked turns a wrong clock into an error, not a wrapped run id
        var request = new GraphStartRequest(record.Id, record.Slug, revision.Revision, occurrence, trigger.ToJsonString(), Concurrency(revision.Spec.Concurrency));
        var started = await starter.Start(request, cancellationToken).ConfigureAwait(false);
        if (started.IsError && started.Error.Code != SindriErrors.GraphRunIdTaken.Code)
        {
            // why: a launch that failed does not use up its tick; the next pass tries it again.
            RetryNextPass(logger ?? NullLogger<WorkflowScheduleService>.Instance, record.Slug.Value, tick, started.Error.Code);
            return started.Error;
        }

        // why: started, queued and skipped ticks are handled (a skipped one must not fire late), and a tick that already has a run is done, not a failure.
        var marked = await runs.MarkTick(record.Id, tick, cancellationToken).ConfigureAwait(false);
        if (marked.IsError)
        {
            return marked.Error;
        }

        return !started.IsError && started.Value.Kind != GraphStartKind.Skipped;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Schedule tick {Tick} of workflow {Slug} did not start ({Code}); the next pass tries it again")]
    private static partial void RetryNextPass(ILogger logger, string slug, DateTimeOffset tick, string code);

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

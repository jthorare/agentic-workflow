using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Features.PollConnections;

/// <summary>
/// The 'workflow-poll' background service (#2258, PR 11), every 30 seconds: it collects the connection_event triggers of every active workflow, groups them by (connection, event kind) so one provider call
/// serves all of them, and polls each group whose connection is due (<c>workflow.poll.intervalSeconds</c>, default 120 s, least 30 s) and not in a backoff. Each event starts one run per trigger whose filter
/// takes it; the run's occurrence is the delivery id (<see cref="DeliveryOccurrence"/>), and the cursor is saved only after the runs started (<see cref="PollRound"/>).
/// On the first run after a control-plane start and after <see cref="PollWake"/> was requested (the machine woke) every group is read forward at once (<see cref="CatchUp"/>), interval or not, but never inside a backoff.
/// An error on one group (token unreadable, answer in a shape the poller doesn't know, a failed start) is logged by code, counted in the run's summary and leaves the others to run; when it came during a catch-up, the
/// catch-up is asked for again. A group whose connection is missing, not connected, or has no poller for its preset is skipped.
/// </summary>
/// <param name="workflows">The workflows and their revisions.</param>
/// <param name="connections">The connections.</param>
/// <param name="pollers">One poller per preset.</param>
/// <param name="cursors">Where cursors are kept.</param>
/// <param name="read">Reads one group, once or forward.</param>
/// <param name="wake">The catch-up request.</param>
/// <param name="approved">The approved profile, which holds the poll keys.</param>
/// <param name="clock">The time.</param>
/// <param name="logger">Where a failed group is logged.</param>
internal sealed partial class PollConnectionsService(
    IWorkflowStore workflows, IConnectionStore connections, IEnumerable<IEventPoller> pollers, IPollCursors cursors, CatchUp read, PollWake wake, IApprovedProfile approved, TimeProvider clock, ILogger<PollConnectionsService>? logger = null)
    : IScheduledService
{
    internal static ServiceName ServiceId { get; } = ServiceName.From("workflow-poll");

    /// <summary>What the service does, as GET /sindri/services says it.</summary>
    public const string Description = "Polls connections for events that start workflows (connection_event triggers), and reads missed events after a start or a wake.";

    /// <summary>Runs without a profile entry, every 30 seconds; each connection is polled on its own interval.</summary>
    internal static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(TimeSpan.FromSeconds(30)), Description);

    public ServiceName Identity() => ServiceId;

    public async ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var catchUp = wake.TryTake();
        var groups = await Groups(cancellationToken).ConfigureAwait(false);
        if (groups.IsError)
        {
            RequestAgain(catchUp);
            return groups.Error;
        }

        if (groups.Value.Count == 0)
        {
            return ScheduledResult.Quiet;
        }

        var settings = await Settings(cancellationToken).ConfigureAwait(false);
        var list = await cursors.List(cancellationToken).ConfigureAwait(false);
        if (list.IsError)
        {
            RequestAgain(catchUp);
            return list.Error;
        }

        var byKey = list.Value.ToDictionary(static c => (c.Connection, c.Kind.Key));
        var polled = 0;
        var started = 0;
        var capped = 0;
        var errors = 0;
        var now = clock.GetUtcNow();
        foreach (var group in groups.Value)
        {
            var cursor = byKey.GetValueOrDefault((group.Connection, group.Kind.Key));
            if (!IsDue(cursor, catchUp, PollSettings.Interval(settings), now))
            {
                continue;
            }

            var outcome = await ReadGroup(group, cursor, catchUp, PollSettings.Pages(settings), run, cancellationToken).ConfigureAwait(false);
            if (outcome.IsError)
            {
                errors++;
                RequestAgain(catchUp);
                if (logger is not null)
                {
                    LogGroupFailed(logger, group.Connection.ToString(), group.Kind.Key, outcome.Error.Code);
                }

                continue;
            }

            if (outcome.Value is not { } done)
            {
                continue;   // no usable connection or poller for this group
            }

            polled++;
            started += done.Started;
            capped += done.Capped ? 1 : 0;
        }

        return polled + errors == 0
            ? ScheduledResult.Quiet
            : new ScheduledResult(Idle: polled > 0 && started + capped + errors == 0, string.Create(CultureInfo.InvariantCulture, $"polled {polled}, started {started} run(s), capped {capped}, failed {errors}"));
    }

    // Due: never polled, or its interval is over; always when a catch-up was asked for. A backoff holds both back.
    private static bool IsDue(PollCursor? cursor, bool catchUp, TimeSpan interval, DateTimeOffset now) =>
        cursor is null
        || ((cursor.BackoffUntil is not { } until || until <= now)
            && (catchUp || cursor.LastPollAt is not { } last || last + interval <= now));

    // A null value: the group can't be polled here (no usable connection or poller), which is not an error.
    private async ValueTask<Result<ReadOutcome?>> ReadGroup(PollGroup group, PollCursor? cursor, bool catchUp, int pages, ScheduledRun run, CancellationToken cancellationToken)
    {
        var found = await connections.Get(group.Connection, cancellationToken).ConfigureAwait(false);
        if (found.IsError || found.Value.Status != ConnectionStatus.Connected || found.Value.Preset is not { } preset)
        {
            return (ReadOutcome?)null;
        }

        var poller = pollers.FirstOrDefault(p => string.Equals(p.Preset, preset, StringComparison.Ordinal));
        if (poller is null)
        {
            return (ReadOutcome?)null;
        }

        var done = await read.Read(group, found.Value, poller, cursor, pages, catchUp, run.Grant, cancellationToken).ConfigureAwait(false);
        return done.IsError ? done.Error : (ReadOutcome?)done.Value;
    }

    private void RequestAgain(bool catchUp)
    {
        if (catchUp)
        {
            wake.Request();   // why: the catch-up did not finish, so the next run reads forward again (a backoff still holds a failing connection back)
        }
    }

    private async ValueTask<PollSettings?> Settings(CancellationToken cancellationToken)
    {
        var latest = await approved.Latest(cancellationToken).ConfigureAwait(false);
        return latest.IsSuccess ? latest.Value?.Profile.Workflow?.Poll : null;
    }

    private async ValueTask<Result<IReadOnlyList<PollGroup>>> Groups(CancellationToken cancellationToken)
    {
        var listed = await workflows.List(includeArchived: false, cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        var found = new Dictionary<(ConnectionName, string), (EventKind Kind, List<PollTrigger> Triggers)>();
        foreach (var record in listed.Value.Where(static r => r.ActiveRevision is not null))
        {
            var revision = await workflows.GetRevision(record.Id, record.ActiveRevision, cancellationToken).ConfigureAwait(false);
            if (revision.IsError)
            {
                return revision.Error;   // why: a store error must not look like "no triggers"; the next run reads again
            }

            var node = revision.Value.Spec.Nodes.FirstOrDefault(static n => string.Equals(n.Type, TriggerMatch.Kind, StringComparison.Ordinal));
            if (node is null || Watched(node.Config) is not { } watched)
            {
                continue;
            }

            var (connection, kind) = watched;

            var trigger = new PollTrigger(record.Id, record.Slug, revision.Value.Revision, Concurrency(revision.Value.Spec.Concurrency), Text(node.Config, "filter"));
            if (!found.TryGetValue((connection, kind.Key), out var group))
            {
                group = (kind, []);
                found[(connection, kind.Key)] = group;
            }

            group.Triggers.Add(trigger);
        }

        return (IReadOnlyList<PollGroup>)[.. found.OrderBy(static p => p.Key.Item1.ToString(), StringComparer.Ordinal).ThenBy(static p => p.Key.Item2, StringComparer.Ordinal)
            .Select(static p => new PollGroup(p.Key.Item1, p.Value.Kind, p.Value.Triggers))];
    }

    // The (connection, kind) a trigger names, or null when its config doesn't name a connection and a catalog event (the validator refuses that when the workflow is saved).
    private static (ConnectionName Connection, EventKind Kind)? Watched(JsonObject config)
    {
        if (Text(config, "connection") is not { } name || Text(config, "event") is not { } key || PollCatalog.Find(key) is not { } kind)
        {
            return null;
        }

        var parsed = ConnectionName.Parse(name);
        return parsed.IsError ? null : (parsed.Value, kind);
    }

    private static string? Text(JsonObject config, string name) => config[name] is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0 ? text : null;

    private static GraphConcurrency Concurrency(string? mode) => mode switch
    {
        "queue" => GraphConcurrency.Queue,
        "unlimited" => GraphConcurrency.Unlimited,
        _ => GraphConcurrency.OnePerWorkflow,
    };

    [LoggerMessage(Level = LogLevel.Warning, Message = "The poll of {Connection} for {Kind} failed ({Code}); the cursor stays and the next poll tries again")]
    private static partial void LogGroupFailed(ILogger logger, string connection, string kind, string code);
}

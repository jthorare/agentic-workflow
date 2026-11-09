using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Features.PollConnections;

/// <summary>
/// The 'workflow-poll' background service (#2258, PR 11), every 30 seconds: it collects the connection_event triggers of every active workflow, groups them by (connection, event kind) so one provider call
/// serves all of them, and polls each group whose connection is due (<c language="text">workflow.poll.intervalSeconds</c>, default 120 s, least 30 s) and not in a backoff. Each event starts one run per trigger whose filter
/// takes it; the run's occurrence is the delivery id (<see cref="DeliveryOccurrence"/>), and the cursor is saved only after the runs started (<see cref="PollRound"/>).
/// On the first run after a control-plane start and after <see cref="PollWake"/> was requested (the machine woke) every group is read forward at once (<see cref="CatchUp"/>), interval or not, but never inside a backoff.
/// An error on one group (token unreadable, answer in a shape the poller doesn't know, a failed start) is logged by code, counted in the run's summary and leaves the others to run; when it came during a catch-up, the
/// catch-up is asked for again. A group whose connection is missing, not connected, or has no poller for its preset is skipped.
/// </summary>
/// <param name="source">Collects the active workflows' triggers into groups.</param>
/// <param name="reader">Reads one group, once or forward.</param>
/// <param name="cursors">Where cursors are kept.</param>
/// <param name="wake">The catch-up request.</param>
/// <param name="approved">The approved profile, which holds the poll keys.</param>
/// <param name="clock">The time.</param>
/// <param name="logger">Where a failed group is logged.</param>
internal sealed partial class PollConnectionsService(
    PollGroupSource source, GroupReader reader, IPollCursors cursors, PollWake wake, IApprovedProfile approved, TimeProvider clock, ILogger<PollConnectionsService>? logger = null)
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
        var groups = await source.Collect(cancellationToken).ConfigureAwait(false);
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
        var totals = new Totals();
        var now = clock.GetUtcNow();
        foreach (var group in groups.Value)
        {
            var cursor = byKey.GetValueOrDefault((group.Connection, group.Kind.Key));
            if (IsDue(cursor, catchUp, PollSettings.Interval(settings), now))
            {
                var outcome = await reader.Read(new GroupRead(group, cursor, catchUp, PollSettings.Pages(settings)), run.Grant, cancellationToken).ConfigureAwait(false);
                Count(totals, group, outcome, catchUp);
            }
        }

        return totals.ToResult();
    }

    // An error counts and asks for the catch-up again; a null value (no usable connection or poller) counts for nothing.
    private void Count(Totals totals, PollGroup group, Result<ReadOutcome?> outcome, bool catchUp)
    {
        if (outcome.IsError)
        {
            totals.Errors++;
            RequestAgain(catchUp);
            if (logger is not null)
            {
                LogGroupFailed(logger, group.Connection.ToString(), group.Kind.Key, outcome.Error.Code);
            }
        }
        else if (outcome.Value is { } done)
        {
            totals.Polled++;
            totals.Started += done.Started;
            totals.Capped += done.Capped ? 1 : 0;
        }
    }

    // Due: never polled, or its interval is over; always when a catch-up was asked for. A backoff holds both back.
    private static bool IsDue(PollCursor? cursor, bool catchUp, TimeSpan interval, DateTimeOffset now) =>
        cursor is null
        || ((cursor.BackoffUntil is not { } until || until <= now)
            && (catchUp || cursor.LastPollAt is not { } last || last + interval <= now));

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

    // The sums of one run, for its summary.
    private sealed class Totals
    {
        internal int Polled { get; set; }

        internal int Started { get; set; }

        internal int Capped { get; set; }

        internal int Errors { get; set; }

        internal ScheduledResult ToResult() => Polled + Errors == 0
            ? ScheduledResult.Quiet
            : new ScheduledResult(Idle: Polled > 0 && Started + Capped + Errors == 0, string.Create(CultureInfo.InvariantCulture, $"polled {Polled}, started {Started} run(s), capped {Capped}, failed {Errors}"));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The poll of {Connection} for {Kind} failed ({Code}); the cursor stays and the next poll tries again")]
    private static partial void LogGroupFailed(ILogger logger, string connection, string kind, string code);
}

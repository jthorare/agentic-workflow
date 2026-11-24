using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Messages;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.RetainMessages;

/// <summary>
/// The nightly 'messages-retention' built-in service (messages spec §8.1): it deletes messages older than messages.retain.days (30) with their FTS rows,
/// but skips a thread (never the whole conversation) that is in Needs reply, has a live snooze or has owner state (a Later or follow flag, a draft, an unsent
/// outbox row). Snooze is read from the ledger's home.row.* events, never views.db, so a views rebuild or a stalled projector can't unsnooze a thread; a
/// snooze event it can't read stops the pass before anything is deleted. Any skip ends after 180 days: the text goes and the row says "text expired".
/// It runs without a profile entry.
/// </summary>
/// <param name="store">messages.db.</param>
/// <param name="snoozes">The live snoozes, from the ledger.</param>
/// <param name="clock">The clock.</param>
internal sealed class RetainMessagesService(IMessagesStore store, LedgerSnoozes snoozes, TimeProvider clock) : IScheduledService
{
    /// <summary>What the service does, as GET /sindri/services says it.</summary>
    public const string Description = "Deletes Slack messages past the retention window, keeping threads that wait on you.";

    /// <summary>messages.retain.days' default (agent default (lead, 2026-11-12)); M1d's profile record makes it a key.</summary>
    internal const int RetainDays = 30;

    /// <summary>The cap on any skip (agent default (lead, 2026-11-12)).</summary>
    internal const int SkipCapDays = 180;

    /// <summary>Gets the service's name in the schedule ('messages-retention', the name aot-smoke's services array pins).</summary>
    internal static ServiceName ServiceId { get; } = ServiceName.From("messages-retention");

    /// <summary>Gets the nightly cadence the scheduler uses without a profile entry.</summary>
    internal static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(TimeSpan.FromHours(24)), Description);

    /// <inheritdoc/>
    public ServiceName Identity() => ServiceId;

    /// <inheritdoc/>
    public async ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var cut = now.AddDays(-RetainDays);
        var threads = await store.RetentionThreads(TsOf(cut), cancellationToken).ConfigureAwait(false);
        if (threads.IsError || threads.Value.Count == 0)
        {
            return threads.IsError ? threads.Error : ScheduledResult.Quiet;
        }

        var snoozed = await snoozes.Live(now, cancellationToken).ConfigureAwait(false);
        if (snoozed.IsError)
        {
            return snoozed.Error;   // a thread it can't classify is kept: nothing is deleted this pass
        }

        var keep = threads.Value.Where(t => t.NeedsReply || t.OwnerState || snoozed.Value.Contains(t.Key)).ToList();
        var expired = await store.Expire(TsOf(cut), TsOf(now.AddDays(-SkipCapDays)), cut, keep, cancellationToken).ConfigureAwait(false);
        if (expired.IsError)
        {
            return expired.Error;
        }

        return expired.Value.MessagesDeleted + expired.Value.OutboxDeleted + expired.Value.TextExpired > 0 ? ScheduledResult.Worked : ScheduledResult.Quiet;
    }

    /// <summary>A time as a Slack ts ('1767771000.000000'), so it compares with stored ts as text.</summary>
    /// <param name="at">The time.</param>
    /// <returns>The ts.</returns>
    internal static MessageTs TsOf(DateTimeOffset at) =>
        new(string.Create(CultureInfo.InvariantCulture, $"{at.ToUnixTimeSeconds():D10}.000000"));
}

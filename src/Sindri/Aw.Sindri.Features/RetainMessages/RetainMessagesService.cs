using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.RetainMessages;

/// <summary>
/// A stub until M1c (messages spec §14, §8.1): the nightly 'messages-retention' service. It deletes messages past messages.retain.days (30), skipping a
/// thread in Needs reply, with a live snooze (read from the ledger's home.row.snoozed and .woke, never views.db) or with owner state; any skip ends after
/// 180 days. Not registered until M1c.
/// </summary>
internal sealed class RetainMessagesService : IScheduledService
{
    /// <summary>Gets the service's name in the schedule ('messages-retention', the name aot-smoke's services array pins).</summary>
    internal static ServiceName ServiceId { get; } = ServiceName.From("messages-retention");

    /// <inheritdoc/>
    public ServiceName Identity() => ServiceId;

    /// <inheritdoc/>
    public ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken) =>
        throw new NotSupportedException("RetainMessages is not built yet: messages spec §14 phase M1c");
}

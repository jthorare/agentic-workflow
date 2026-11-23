using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.SyncMessages;

/// <summary>
/// A stub until M1e (messages spec §14, §5.2, §5.3): the 'messages-sync' service. PollLive only in Release 1: the hot set, the search detector, the
/// thread-reply check (20% of the bucket), the cold-start fill, gap markers, status words and 'messages.synced' at its rate. Not registered until M1e.
/// </summary>
internal sealed class SyncMessagesService : IScheduledService
{
    /// <summary>Gets the service's name in the schedule ('messages-sync', the name aot-smoke's services array pins).</summary>
    internal static ServiceName ServiceId { get; } = ServiceName.From("messages-sync");

    /// <inheritdoc/>
    public ServiceName Identity() => ServiceId;

    /// <inheritdoc/>
    public ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken) =>
        throw new NotSupportedException("SyncMessages is not built yet: messages spec §14 phase M1e");
}

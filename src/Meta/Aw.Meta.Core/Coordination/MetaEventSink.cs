using Aw.Meta.Core.Events;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Coordination;

/// <summary>Appends events for the commands that decide a fact. An append that fails never fails the caller.</summary>
/// <param name="store">The event log.</param>
public sealed class MetaEventSink(IMetaEventStore store)
{
    /// <summary>Appends one event.</summary>
    /// <param name="metaEvent">The event.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that ends when the append ended, whether it worked or not.</returns>
    public async ValueTask Emit(MetaEvent metaEvent, CancellationToken cancellationToken) =>
        // Advisory, like the running log: the command's own work is done, and a missed event is rebuilt from the next snapshot.
        _ = await store.Append(metaEvent, cancellationToken).ConfigureAwait(false);
}

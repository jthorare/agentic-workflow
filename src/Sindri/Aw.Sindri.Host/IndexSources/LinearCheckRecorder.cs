using Aw.Kernel;
using Aw.Sindri.Features.LinearConnection;

namespace Aw.Sindri.Host.IndexSources;

/// <summary>Keeps the result of a read-key check made outside Settings (`sindri index connect linear`), stamped with the clock, so Settings shows the key as checked and not as 'unchecked'.</summary>
/// <param name="store">Keeps the check.</param>
/// <param name="clock">The clock.</param>
internal sealed class LinearCheckRecorder(ILinearCheckStore store, TimeProvider clock)
{
    /// <summary>Records that the read key at <paramref name="pointer"/> was just checked against Linear and read.</summary>
    /// <param name="pointer">The pointer now in the profile.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or the store's error.</returns>
    internal ValueTask<Result<Unit>> ReadResolved(string pointer, CancellationToken cancellationToken) =>
        store.RecordRead(new LinearStoredCheck(pointer, "resolves", clock.GetUtcNow()), cancellationToken);
}

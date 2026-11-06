using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>An IDelay that runs a hook after each wait, as a lane's own hook would while the meta waits: the wait moves the manual clock, then <paramref name="afterWait"/> gets the call number (from 1) and the new time.</summary>
/// <param name="inner">The delay that moves the clock.</param>
/// <param name="clock">The clock the wait moved.</param>
/// <param name="afterWait">Runs after each wait.</param>
public sealed class HookingDelay(InMemoryDelay inner, ManualClock clock, Func<int, DateTimeOffset, ValueTask> afterWait) : IDelay
{
    private int _calls;

    /// <inheritdoc />
    public async ValueTask Wait(TimeSpan duration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inner);
        await inner.Wait(duration, cancellationToken).ConfigureAwait(false);
        _calls++;
        await afterWait(_calls, clock.GetUtcNow()).ConfigureAwait(false);
    }
}

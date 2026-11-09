namespace Aw.Sindri.Host.Endpoints;

/// <summary>At most <see cref="PerMinute"/> handoffs in each minute (a fixed window), for POST /sindri/handoff.</summary>
/// <param name="clock">The clock.</param>
internal sealed class HandoffRateLimit(TimeProvider clock)
{
    internal const int PerMinute = 30;

    private readonly Lock _gate = new();
    private long _window = -1;
    private int _count;

    /// <summary>Counts one handoff.</summary>
    /// <returns>True when it may go on, false when the minute's allowance is used up.</returns>
    internal bool TryTake()
    {
        var window = clock.GetUtcNow().ToUnixTimeSeconds() / 60;
        lock (_gate)
        {
            if (window != _window)
            {
                (_window, _count) = (window, 0);
            }

            return ++_count <= PerMinute;
        }
    }
}

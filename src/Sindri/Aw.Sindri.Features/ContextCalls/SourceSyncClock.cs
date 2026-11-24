namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// How long this serve has run, for the source look's first-pass limit (#3832). The host builds it as serve starts (SourceSyncClockStart), so the
/// count starts then, not at the first status read. In memory: a restart starts the count again.
/// </summary>
internal sealed class SourceSyncClock
{
    private readonly TimeProvider _clock;
    private readonly DateTimeOffset _start;

    /// <summary>Starts the count now.</summary>
    /// <param name="clock">The clock.</param>
    public SourceSyncClock(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
        _start = clock.GetUtcNow();
    }

    /// <summary>How long serve has run.</summary>
    /// <returns>The time since serve started.</returns>
    internal TimeSpan Running() => _clock.GetUtcNow() - _start;

    /// <summary>How long since the later of serve's start and <paramref name="resumed"/> (#3909): a resume restarts the count, one before serve started doesn't.</summary>
    /// <param name="resumed">When index-sync was last resumed, or null.</param>
    /// <returns>The time since that later moment, never below zero.</returns>
    internal TimeSpan Since(DateTimeOffset? resumed)
    {
        var from = resumed is { } at && at > _start ? at : _start;
        var since = _clock.GetUtcNow() - from;
        return since < TimeSpan.Zero ? TimeSpan.Zero : since;
    }

    /// <summary>The time now, on the same clock.</summary>
    /// <returns>The time now.</returns>
    internal DateTimeOffset Now() => _clock.GetUtcNow();
}

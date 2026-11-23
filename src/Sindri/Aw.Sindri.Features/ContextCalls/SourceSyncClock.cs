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
}

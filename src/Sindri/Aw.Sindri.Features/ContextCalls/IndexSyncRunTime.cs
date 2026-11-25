using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The run time the source look counts against its first-pass limit: one read for the index status and the status health (#4010).</summary>
internal static class IndexSyncRunTime
{
    /// <summary>How long index-sync has run: since the later of serve's start and its last resume, or zero while it is paused. A paused index-sync
    /// finishes no pass, so counting from serve's start would flag every new source right after a long pause (#3909). A pause read that fails counts
    /// as running, as MainMovePause reads it; a resume read that fails counts from serve's start.</summary>
    /// <param name="started">This serve's clock.</param>
    /// <param name="pauses">The paused services.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The time to pass to <see cref="IndexSourceLook"/>.</returns>
    internal static async ValueTask<TimeSpan> For(SourceSyncClock started, IServicePauses pauses, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(started);
        ArgumentNullException.ThrowIfNull(pauses);
        var paused = await pauses.Paused(cancellationToken).ConfigureAwait(false);
        if (paused.IsSuccess && paused.Value.Contains(IndexSyncService.ServiceId))
        {
            return TimeSpan.Zero;
        }

        var resumed = await pauses.ResumedAt(IndexSyncService.ServiceId, cancellationToken).ConfigureAwait(false);
        return started.Since(resumed.IsSuccess ? resumed.Value : null);
    }
}

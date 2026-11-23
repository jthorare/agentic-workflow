using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The run time the source look counts against its first-pass limit: one read for the index status and the status health (#4010).</summary>
internal static class IndexSyncRunTime
{
    /// <summary>How long serve has run, or zero while index-sync is paused: a paused index-sync finishes no pass, so the limit would flag every new
    /// source (#3909). A pause read that fails counts as running, as MainMovePause reads it.</summary>
    /// <param name="started">This serve's clock.</param>
    /// <param name="pauses">The paused services.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The time to pass to <see cref="IndexSourceLook"/>.</returns>
    internal static async ValueTask<TimeSpan> For(SourceSyncClock started, IServicePauses pauses, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(started);
        ArgumentNullException.ThrowIfNull(pauses);
        var paused = await pauses.Paused(cancellationToken).ConfigureAwait(false);
        return paused.IsSuccess && paused.Value.Contains(IndexSyncService.ServiceId) ? TimeSpan.Zero : started.Running();
    }
}

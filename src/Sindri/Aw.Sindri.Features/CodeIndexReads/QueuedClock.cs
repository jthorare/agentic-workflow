using System.Collections.Concurrent;

namespace Aw.Sindri.Features.CodeIndexReads;

/// <summary>
/// When this serve first read each repo as queued (#3440), so a first build that never starts turns into a problem. In memory: a restart starts every count again, as it restarts the scheduler that starts the build.
/// </summary>
internal sealed class QueuedClock
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _since = new(StringComparer.Ordinal);

    /// <summary>How long the repo has read queued, counted from the first read that saw it queued; a read that sees it in any other state forgets it.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="queued">Whether this read sees the repo queued.</param>
    /// <param name="now">The time of this read.</param>
    /// <returns>The time spent queued, or null when the repo isn't queued.</returns>
    internal TimeSpan? For(string repo, bool queued, DateTimeOffset now)
    {
        _ = (repo, queued, now, _since.Count);   // red stub: counts nothing yet
        return null;
    }
}

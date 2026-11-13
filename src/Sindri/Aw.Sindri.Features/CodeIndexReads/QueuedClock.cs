using System.Collections.Concurrent;

namespace Aw.Sindri.Features.CodeIndexReads;

/// <summary>
/// When each repo went queued (#3440), so a first build that never starts turns into a problem. A repo this serve has never read before counts from serve start (#3480); one that leaves queued and comes back counts from that read.
/// In memory: a restart starts every count again, as it restarts the scheduler that starts the build.
/// </summary>
/// <param name="start">When this serve started.</param>
internal sealed class QueuedClock(DateTimeOffset start)
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _since = new(StringComparer.Ordinal);

    /// <summary>How long the repo has read queued, counted from the first read that saw it queued; a read that sees it in any other state forgets it.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="queued">Whether this read sees the repo queued.</param>
    /// <param name="now">The time of this read.</param>
    /// <returns>The time spent queued, or null when the repo isn't queued.</returns>
    internal TimeSpan? For(string repo, bool queued, DateTimeOffset now)
    {
        _ = start;
        if (!queued)
        {
            _ = _since.TryRemove(repo, out _);
            return null;
        }

        return now - _since.GetOrAdd(repo, now);
    }
}

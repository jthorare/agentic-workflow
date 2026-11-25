using System.Collections.Concurrent;

namespace Aw.Sindri.Features.CodeIndexReads;

/// <summary>
/// When each repo went queued (#3440), so a first build that never starts turns into a problem. A repo the profile named at serve start
/// (<see cref="Seed"/>) counts from serve start on its first read (#3480); a repo added later, and one that leaves queued and comes back, counts
/// from the read that first sees it queued, so a fresh 'repo add' never reads 30 min late. In memory: a restart starts every count again, as it
/// restarts the scheduler that starts the build.
/// </summary>
/// <param name="start">When this serve started.</param>
internal sealed class QueuedClock(DateTimeOffset start)
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _since = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _atStart = new(StringComparer.Ordinal);

    /// <summary>Records the repos the profile names as serve starts: only these count from serve start, once, on their first read.</summary>
    /// <param name="repos">The repo names.</param>
    internal void Seed(IEnumerable<string> repos)
    {
        ArgumentNullException.ThrowIfNull(repos);
        foreach (var repo in repos)
        {
            _ = _atStart.TryAdd(repo, 0);
        }
    }

    /// <summary>How long the repo has read queued: from serve start when it was seeded and this first read sees it queued, else from the read that first saw it queued; a read that sees it in any other state forgets it.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="queued">Whether this read sees the repo queued.</param>
    /// <param name="now">The time of this read.</param>
    /// <returns>The time spent queued, or null when the repo isn't queued.</returns>
    internal TimeSpan? For(string repo, bool queued, DateTimeOffset now)
    {
        var fromStart = _atStart.TryRemove(repo, out _);
        if (!queued)
        {
            _ = _since.TryRemove(repo, out _);
            return null;
        }

        // why: a repo there at serve start and queued on its first read has waited since serve start, not since someone first looked (#3480);
        // a repo added mid-serve has waited only since that read (#3502 review)
        return now - _since.GetOrAdd(repo, fromStart ? start : now);
    }
}

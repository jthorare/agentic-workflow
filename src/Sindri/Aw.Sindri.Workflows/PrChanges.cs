using Aw.Sindri.Core.Forge;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// What changed on a pull request between two polls (PR babysitter spec §6.1): the outcome is the first change in the table's order, and every change is listed.
/// Changes are read from the digest's typed fields (state, head, checks, comment count); the version token is compared for equality only, so a move
/// of it that none of those explain is a 'review' change, since nothing more is known.
/// </summary>
internal static class PrChanges
{
    private static readonly string[] _order = ["merged", "closed", "updated", "review", "ci"];

    /// <summary>The changes of a poll against the last one.</summary>
    /// <param name="before">What the poller saw last, or null for a pull request it sees the first time.</param>
    /// <param name="now">The digest now.</param>
    /// <returns>The changes in table order ('merged', 'closed', 'updated', 'review', 'ci'); empty when nothing changed. A first sight changes only when the pull request is already merged or closed.</returns>
    public static IReadOnlyList<string> Between(PrWatchEntry? before, PullDigestRow now)
    {
        ArgumentNullException.ThrowIfNull(now);
        var found = new HashSet<string>(StringComparer.Ordinal);
        if (before is null)
        {
            Ended(now.State, found);
        }
        else if (Moved(before, now))
        {
            Compare(before, now, found);
        }

        return [.. _order.Where(found.Contains)];
    }

    /// <summary>What the poller keeps of a digest.</summary>
    /// <param name="now">The digest.</param>
    /// <param name="at">When the pull request last changed.</param>
    /// <returns>The entry.</returns>
    public static PrWatchEntry Entry(PullDigestRow now, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(now);
        return new PrWatchEntry(now.Version, now.HeadSha, now.State.ToString(), now.Checks.ToString(), now.CommentCount, at);
    }

    private static bool Moved(PrWatchEntry before, PullDigestRow now) =>
        !string.Equals(before.Version, now.Version, StringComparison.Ordinal)
        || !string.Equals(before.Checks, now.Checks.ToString(), StringComparison.Ordinal)
        || before.CommentCount != now.CommentCount
        || !string.Equals(before.HeadSha, now.HeadSha, StringComparison.Ordinal)
        || !string.Equals(before.State, now.State.ToString(), StringComparison.Ordinal);

    private static void Compare(PrWatchEntry before, PullDigestRow now, HashSet<string> found)
    {
        if (!string.Equals(before.State, now.State.ToString(), StringComparison.Ordinal))
        {
            Ended(now.State, found);
            if (now.State == PullState.Open)
            {
                _ = found.Add("updated");   // reopened: the pull request is live again
            }
        }

        if (!string.Equals(before.HeadSha, now.HeadSha, StringComparison.Ordinal))
        {
            _ = found.Add("updated");
        }

        if (!string.Equals(before.Checks, now.Checks.ToString(), StringComparison.Ordinal))
        {
            _ = found.Add("ci");
        }

        if (before.CommentCount != now.CommentCount)
        {
            _ = found.Add("review");
        }

        if (found.Count == 0)
        {
            _ = found.Add("review");
        }
    }

    private static void Ended(PullState state, HashSet<string> found)
    {
        if (state == PullState.Merged)
        {
            _ = found.Add("merged");
        }
        else if (state == PullState.Closed)
        {
            _ = found.Add("closed");
        }
    }
}

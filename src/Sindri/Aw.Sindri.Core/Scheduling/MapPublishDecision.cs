namespace Aw.Sindri.Core.Scheduling;

/// <summary>When a refreshed context map becomes the current one (owner decision 2026-10-20, code index spec 6.4): clock in, nothing out. A draft built before its code index is rebuilt once the index is in, never published (#1704).</summary>
public static class MapPublishDecision
{
    /// <summary>Decides, first applicable line wins.</summary>
    /// <param name="input">The hold, the draft and current numbers, the outlooks and the wait.</param>
    /// <param name="now">The time.</param>
    /// <returns>Wait, publish or drop.</returns>
    public static MapPublishAction Decide(MapPublishInput input, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(input);
        var hold = input.Hold;
        if (input.CurrentVersion == hold.Version)
        {
            return new MapDrop(MapDropReason.AlreadyCurrent);
        }

        if (input.DraftVersion != hold.Version)
        {
            return new MapDrop(MapDropReason.Gone);
        }

        var gated = Gated(input);
        List<string> notBuilt = [.. gated.Where(static r => r.Outlook != IndexOutlook.Built).Select(static r => r.Repo)];
        if (gated.Count == 0)
        {
            return new MapPublish(MapPublishReason.NoIndex, []);
        }

        if (notBuilt.Count == 0)
        {
            // #1704: every index is in, but a draft built before its repo's index has unresolved symbol citations; it is built again, never published.
            List<string> unindexed = [.. gated.Where(r => !input.Hold.Heads[r.Repo].BuiltWithIndex).Select(static r => r.Repo)];
            return unindexed.Count > 0 ? new MapRebuild(unindexed) : new MapPublish(MapPublishReason.IndexReady, []);
        }

        return gated.Exists(static r => r.Outlook == IndexOutlook.Broken)
            ? new MapPublish(MapPublishReason.IndexBroken, notBuilt)
            : WithinTheWait(input, notBuilt, now);
    }

    // Absent repos are not part of the gate; a repo of the hold with no outlook reads as absent.
    private static List<(string Repo, IndexOutlook Outlook)> Gated(MapPublishInput input) =>
        [.. input.Hold.Heads.Keys
            .Select(repo => (Repo: repo, Outlook: input.Outlooks.TryGetValue(repo, out var detail) ? detail.Outlook : IndexOutlook.Absent))
            .Where(static r => r.Outlook != IndexOutlook.Absent)
            .OrderBy(static r => r.Repo, StringComparer.Ordinal)];

    // Lines 4 to 7: the first map, waiting turned off, the bound (inclusive; a since in the future is clamped to now), else keep waiting.
    private static MapPublishAction WithinTheWait(MapPublishInput input, List<string> notBuilt, DateTimeOffset now)
    {
        if (input.CurrentVersion is null)
        {
            return new MapPublish(MapPublishReason.FirstMap, notBuilt);
        }

        if (input.Wait <= TimeSpan.Zero)
        {
            return new MapPublish(MapPublishReason.WaitOff, notBuilt);
        }

        var until = (input.Hold.Since < now ? input.Hold.Since : now) + input.Wait;
        return now >= until ? new MapPublish(MapPublishReason.TimedOut, notBuilt) : new MapWait(until, notBuilt.Count);
    }
}

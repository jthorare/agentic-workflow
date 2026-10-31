namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The live profile: its hash and the key paths that differ from the approved one ('index.pages.schedule-week', 'repos.web.index.pages.x').</summary>
/// <param name="LiveHash">The live hash, or null when none was read.</param>
/// <param name="ChangedPaths">The waiting key paths.</param>
internal sealed record ProfileWaiting(string? LiveHash, IReadOnlyList<string> ChangedPaths)
{
    /// <summary>Gets a value indicating whether every waiting key path is one the caller wrote, so the app may approve: nothing waits, or each waiting path satisfies <paramref name="mine"/>.</summary>
    /// <param name="mine">Says whether a waiting key path is the caller's.</param>
    /// <returns>True when nothing but the caller's paths waits.</returns>
    internal bool OnlyMine(Func<string, bool> mine)
    {
        ArgumentNullException.ThrowIfNull(mine);
        return ChangedPaths.All(mine);
    }

    /// <summary>Gets a value indicating whether only the given pages' paths wait, so the app may approve: nothing waits, or every waiting path is one of theirs.</summary>
    /// <param name="pageIds">The page ids this write sets or removes.</param>
    /// <returns>True when nothing but these pages waits.</returns>
    internal bool OnlyPages(IEnumerable<string> pageIds)
    {
        var ids = pageIds.ToList();
        return OnlyMine(p => IsPage(p, ids));
    }

    /// <summary>Gets a value indicating whether a page waits for approval (its 'Waiting for approval' mark, pages spec §3.5).</summary>
    /// <param name="pageId">The page id.</param>
    /// <returns>True when a path of that page waits.</returns>
    internal bool Waits(string pageId) => ChangedPaths.Any(p => p.EndsWith(".pages." + pageId, StringComparison.Ordinal));

    /// <summary>Gets a value indicating whether a key path is one of the given pages' ('index.pages.x', 'repos.web.index.pages.x').</summary>
    /// <param name="path">The key path.</param>
    /// <param name="pageIds">The page ids.</param>
    /// <returns>True when the path ends with '.pages.' and one of the ids.</returns>
    internal static bool IsPage(string path, IEnumerable<string> pageIds) => pageIds.Any(id => path.EndsWith(".pages." + id, StringComparison.Ordinal));
}

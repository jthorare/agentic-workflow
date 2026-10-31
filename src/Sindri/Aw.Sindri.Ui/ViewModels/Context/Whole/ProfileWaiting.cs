namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The live profile: its hash and the key paths that differ from the approved one ('index.pages.schedule-week', 'repos.web.index.pages.x').</summary>
/// <param name="LiveHash">The live hash, or null when none was read.</param>
/// <param name="ChangedPaths">The waiting key paths.</param>
internal sealed record ProfileWaiting(string? LiveHash, IReadOnlyList<string> ChangedPaths)
{
    /// <summary>Gets a value indicating whether only the given pages' paths wait, so the app may approve: nothing waits, or every waiting path is one of theirs.</summary>
    /// <param name="pageIds">The page ids this write sets or removes.</param>
    /// <returns>True when nothing but these pages waits.</returns>
    internal bool OnlyPages(IEnumerable<string> pageIds)
    {
        var ids = pageIds.ToHashSet(StringComparer.Ordinal);
        return ChangedPaths.All(p => p.LastIndexOf(".pages.", StringComparison.Ordinal) is var at and >= 0 && ids.Contains(p[(at + ".pages.".Length)..]));
    }

    /// <summary>Gets a value indicating whether a page waits for approval (its 'Waiting for approval' mark, pages spec §3.5).</summary>
    /// <param name="pageId">The page id.</param>
    /// <returns>True when a path of that page waits.</returns>
    internal bool Waits(string pageId) => ChangedPaths.Any(p => p.EndsWith(".pages." + pageId, StringComparison.Ordinal));
}

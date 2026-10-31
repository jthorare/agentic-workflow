using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Pages;
using Aw.Kernel;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.SetPages;

/// <summary>The write rules of pages (pages spec §3.1) over the pages as they will be after a SetPages request.</summary>
internal static class PageWriteCheck
{
    // The write rules (the same ones the app's sheet and Add several pages call) over every planned page, without the read-time caps, then the profile's own.
    internal static Error? Check(SindriProfile planned, List<PageSlot> slots, PinNames pins, PageMapFacts map)
    {
        var facts = map.Facts with
        {
            Stored = EntryKey.Normalized,
            ConceptIds = new HashSet<ConceptId>(map.Facts.ConceptIds.Concat(pins.Ids.Where(static i => ConceptId.TryFrom(i, out _)).Select(static i => ConceptId.From(i)))),
        };
        var live = slots.Where(static s => !s.Removed).ToList();
        var taken = new HashSet<string>(live.Select(static s => s.Entry.Id?.Value).OfType<string>(), StringComparer.Ordinal);
        var errors = PageRules.Check([.. live.Select(s => Pin(s, taken))], facts);
        if (errors.Count > 0)
        {
            return errors[0];
        }

        var checkedProfile = ProfileRules.Check(planned);
        return checkedProfile.IsError ? checkedProfile.Error : null;
    }

    // An explicit id is used as written, so a repeat reaches the duplicate rule; an id the build makes from the name takes '-2', '-3' as the build does.
    private static PagePin Pin(PageSlot slot, HashSet<string> taken)
    {
        var page = slot.Entry;
        var name = page.Name ?? string.Empty;
        var repo = slot.Repo ?? page.Repo;
        return new PagePin(PageId.From(Own(page.Id?.Value, name, taken)), name, RepoOf(repo), page.Surface, Actions(page));
    }

    private static ContextRepoName? RepoOf(string? repo) => repo is not null && ContextRepoName.TryFrom(repo, out var name) ? name : null;

    private static List<PageActionPin> Actions(PageSettings page)
    {
        var taken = new HashSet<string>((page.Actions ?? []).Select(static a => a.Id?.Value).OfType<string>(), StringComparer.Ordinal);
        return [.. (page.Actions ?? []).Select(a => new PageActionPin(PageActionId.From(Own(a.Id?.Value, a.Name, taken)), a.Name ?? string.Empty, a.Action, a.Concepts ?? []))];
    }

    private static string Own(string? written, string? name, HashSet<string> taken) =>
        written is not null && PageId.TryFrom(written, out _) ? written : PageSlugs.Unique(PageSlugs.Of(name), taken);
}

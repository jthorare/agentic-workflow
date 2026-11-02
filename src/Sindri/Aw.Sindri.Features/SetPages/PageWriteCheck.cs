using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Kernel;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.SetPages;

/// <summary>The write rules of pages (pages spec §3.1) over the pages as they will be after a SetPages request.</summary>
internal static partial class PageWriteCheck
{
    // The write rules (the same ones the app's sheet and Add several pages call) over every planned page, then the profile's own. Caps, duplicate ids and names are checked over the whole list; the map's keys only for the pages this request set, since the facts are the caller's view and may not hold another repo's pages.
    internal static Error? Check(SindriProfile planned, List<PageSlot> slots, PinNames pins, MapPageFacts map)
    {
        var ids = map.Concepts.Select(static c => c.Key).Concat(pins.Ids).Where(static i => ConceptId.TryFrom(i, out _)).Select(static i => ConceptId.From(i));
        var facts = new PageFacts(new HashSet<ConceptId>(ids), map.SurfaceRepos, map.ActionRepos) { Stored = EntryKey.Normalized };   // the MUST: a key typed with capitals finds the map's lower-cased one
        var live = slots.Where(static s => !s.Removed).ToList();
        var taken = new HashSet<string>(live.Select(static s => s.Entry.Id?.Value).OfType<string>(), StringComparer.Ordinal);
        var errors = PageRules.Check([.. live.Select(s => Pin(s, taken))], facts);
        if (errors.Count > 0)
        {
            return Located(errors[0], live);
        }

        var checkedProfile = ProfileRules.Check(planned);
        return checkedProfile.IsError ? checkedProfile.Error : null;
    }

    // PageRules counts pages in the merged list; the owner's file has the top-level list and each repo's own, so the paths are rewritten to where the page really is.
    private static Error Located(Error error, List<PageSlot> live) =>
        error with { Message = MergedIndex.Replace(error.Message, m => PagePlanner.PathOf(live, int.Parse(m.Groups["n"].Value, CultureInfo.InvariantCulture))) };

    [GeneratedRegex(@"index\.pages\[(?<n>\d+)\]", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MergedIndex { get; }

    // An explicit id is used as written, so a repeat reaches the duplicate rule; an id the build makes from the name takes '-2', '-3' as the build does.
    private static PagePin Pin(PageSlot slot, HashSet<string> taken)
    {
        var page = slot.Entry;
        var name = page.Name ?? string.Empty;
        var repo = slot.Repo ?? page.Repo;
        var id = PageId.From(Own(page.Id?.Value, name, taken));
        return slot.Touched
            ? new PagePin(id, name, RepoOf(repo), page.Surface, Actions(page, keyed: true))
            : new PagePin(id, name, RepoOf(repo), SurfaceKey: null, Actions(page, keyed: false));
    }

    private static ContextRepoName? RepoOf(string? repo) => repo is not null && ContextRepoName.TryFrom(repo, out var name) ? name : null;

    private static List<PageActionPin> Actions(PageSettings page, bool keyed)
    {
        var taken = new HashSet<string>((page.Actions ?? []).Select(static a => a.Id?.Value).OfType<string>(), StringComparer.Ordinal);
        return [.. (page.Actions ?? []).Select(a => new PageActionPin(PageActionId.From(Own(a.Id?.Value, a.Name, taken)), a.Name ?? string.Empty, keyed ? a.Action : null, keyed ? a.Concepts ?? [] : []))];
    }

    private static string Own(string? written, string? name, HashSet<string> taken) =>
        written is not null && PageId.TryFrom(written, out _) ? written : PageSlugs.Unique(PageSlugs.Of(name), taken);
}

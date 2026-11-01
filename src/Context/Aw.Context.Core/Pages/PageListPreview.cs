using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Pages;

/// <summary>Matches pasted blocks against the map's pages and checks them against the page rules, without writing anything.</summary>
public static partial class PageListPreview
{
    private const string Suggested = "suggested";
    private const string Owner = "owner";
    private const string ConceptPrefix = "concept:";

    /// <summary>Previews the blocks.</summary>
    /// <param name="blocks">The parsed paste.</param>
    /// <param name="pages">Every page the caller sees (owner and suggested).</param>
    /// <param name="concepts">Pins first, then the map's concepts, each (id, name).</param>
    /// <param name="ownerPages">How many pages the profile names (the 200-page cap counts them); null counts the owner pages in <paramref name="pages"/>.</param>
    /// <returns>One row per block.</returns>
    public static IReadOnlyList<PageListRow> Of(IReadOnlyList<PageListBlock> blocks, IReadOnlyList<PageRow> pages, IReadOnlyList<(ConceptId Id, string Name)> concepts, int? ownerPages = null)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(concepts);
        var suggested = pages.Where(static p => p.Mark == Suggested).Select(static p => (Row: p, Words: AnchorMatch.Words(p.Name))).ToList();
        var owned = pages.Where(static p => p.Mark == Owner).Select(static p => p.Id).ToHashSet();
        var facts = new PageFacts(concepts.Select(static c => c.Id).ToHashSet(), new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal), new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal));
        var rows = new List<PageListRow>(blocks.Count);
        var ids = new List<PageId>(blocks.Count);
        foreach (var block in blocks)
        {
            var (row, id) = BlockRow(block, (suggested, owned, facts, concepts), rows, ids);
            ids.Add(id);
            rows.Add(row);
        }

        return Capped(rows, ownerPages ?? owned.Count);
    }

    private static (PageListRow Row, PageId Id) BlockRow(
        PageListBlock block, (List<(PageRow Row, HashSet<string> Words)> Suggested, HashSet<PageId> Owned, PageFacts Facts, IReadOnlyList<(ConceptId Id, string Name)> Concepts) world, List<PageListRow> rows, List<PageId> ids)
    {
        var page = MatchPage(block.Name, world.Suggested);
        var match = page is null ? null : new PageListMatch(page.Key, page.Id, page.Name, page.Actions.Count);
        var id = page?.Id ?? PageId.From(PageSlugs.Of(block.Name));
        var sameAs = SameAs(rows, ids, match, id);
        var actions = block.Actions.Select(a => ActionRow(a, page, world.Concepts)).ToList();
        var unknown = actions.SelectMany(static a => a.Concepts).Where(static c => c.Id is null).Select(static c => c.Written).Distinct(StringComparer.Ordinal).ToList();
        var pin = Pin(block, id, page, actions);
        var problem = Reason(sameAs, pin, world.Facts, block, actions);
        var reason = problem ?? (unknown.Count > 0 ? $"No concept called \"{unknown[0]}\": pick one" : null);
        // A row whose only problem is an unknown concept keeps its page (the concept as written), so the app can swap in the owner's pick and save it.
        return (new PageListRow(
            block.Block, block.Line, block.Name, match, world.Owned.Contains(id) ? id : null, sameAs, reason is null, reason, actions, unknown, problem is null ? pin : null), id);
    }

    // The profile holds at most 200 pages: walking the rows that carry a page in order (an unknown-concept row is unticked but still adds its page), one that would pass the cap is not added.
    private static List<PageListRow> Capped(List<PageListRow> rows, int owners)
    {
        var added = 0;
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].Page is null || rows[i].Replaces is not null)
            {
                continue;
            }

            if (owners + added + 1 > PageRules.MaxPages)
            {
                rows[i] = rows[i] with { Ticked = false, Reason = $"Not added: The profile holds at most {Count(PageRules.MaxPages)} pages", Page = null };
            }
            else
            {
                added++;
            }
        }

        return rows;
    }

    // The first earlier block with the same match key, or the same id.
    private static int? SameAs(List<PageListRow> rows, List<PageId> ids, PageListMatch? match, PageId id)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            if ((match is not null && rows[i].Match?.Key == match.Key) || ids[i] == id)
            {
                return rows[i].Block;
            }
        }

        return null;
    }

    // The row's one sentence before concepts: a repeated page, then a name, size or repeat problem, then two lines naming one map action.
    private static string? Reason(int? sameAs, PagePin pin, PageFacts facts, PageListBlock block, List<PageListActionRow> actions) =>
        sameAs is { } first ? $"Same page as block {Count(first)}" : Problem(pin, facts, block.Actions) ?? SameAction(block, actions);

    private static string? SameAction(PageListBlock block, List<PageListActionRow> actions)
    {
        for (var i = 1; i < actions.Count; i++)
        {
            for (var j = 0; j < i; j++)
            {
                if (actions[i].Match is { } match && actions[j].Match?.Key == match.Key)
                {
                    return $"Not added: {block.Actions[j].Name} and {block.Actions[i].Name} are the same action";
                }
            }
        }

        return null;
    }

    private static PageRow? MatchPage(string name, List<(PageRow Row, HashSet<string> Words)> suggested)
    {
        var words = AnchorMatch.Words(name);
        if (words.Count == 0)
        {
            return null;
        }

        return suggested.Where(s => s.Words.SetEquals(words)).OrderByDescending(static s => s.Row.Actions.Count).ThenBy(static s => s.Row.Key.Value, StringComparer.Ordinal).Select(static s => s.Row).FirstOrDefault()
            ?? suggested.Select(s => (s.Row, Shared: s.Words.Count(words.Contains))).Where(static s => s.Shared > 0)
                .OrderByDescending(static s => s.Shared).ThenByDescending(static s => s.Row.Actions.Count).ThenBy(static s => s.Row.Key.Value, StringComparer.Ordinal).Select(static s => s.Row).FirstOrDefault();
    }

    private static PageListActionRow ActionRow(PageListAction action, PageRow? page, IReadOnlyList<(ConceptId Id, string Name)> concepts)
    {
        var words = AnchorMatch.Words(action.Name);
        var match = page is null || words.Count == 0 ? null
            : page.Actions.Where(a => AnchorMatch.Words(a.Name).SetEquals(words)).OrderBy(static a => a.Key.Value, StringComparer.Ordinal).FirstOrDefault();
        var matched = match is null ? null : new PageListActionMatch(match.Key, match.Name);
        if (match is not null && action.Concepts is null)
        {
            return new PageListActionRow(action.Name, matched, [.. match.Concepts.Select(static c => new PageListConcept(c, ConceptId.From(c)))]);
        }

        return new PageListActionRow(action.Name, matched, [.. (action.Concepts ?? []).Select(c => new PageListConcept(c, Resolve(c, concepts)))]);
    }

    // As PagePlanner.Concept does: an id (with or without 'concept:') the pins or the map have, else an exact name ignoring case, pins before the map.
    private static ConceptId? Resolve(string written, IReadOnlyList<(ConceptId Id, string Name)> concepts)
    {
        var bare = written.StartsWith(ConceptPrefix, StringComparison.Ordinal) ? written[ConceptPrefix.Length..] : written;
        if (ConceptId.TryFrom(bare, out var id) && concepts.Any(c => c.Id == id))
        {
            return id;
        }

        foreach (var (conceptId, name) in concepts)
        {
            if (string.Equals(name, written, StringComparison.OrdinalIgnoreCase))
            {
                return conceptId;
            }
        }

        return null;
    }

    private static PagePin Pin(PageListBlock block, PageId id, PageRow? row, List<PageListActionRow> actions)
    {
        var pinned = actions.Select(a => new PageActionPin(
            PageActionId.From(PageSlugs.Of(a.Name)), a.Name, a.Match?.Key.Value, [.. a.Concepts.Select(static c => c.Id?.Value ?? c.Written)])).ToList();
        var repo = row?.Repo is { } name && ContextRepoName.TryFrom(name, out var parsed) ? parsed : (ContextRepoName?)null;
        return new PagePin(id, block.Name, repo, row?.Key.Value, pinned);
    }

    // The first of the page rules' size, name and repeat problems, as the row's sentence; the rules themselves run in PageRules.Check.
    private static string? Problem(PagePin pin, PageFacts facts, IReadOnlyList<PageListAction> written)
    {
        var codes = new[] { ContextErrors.PageOverCap.Code, ContextErrors.PageIdDuplicate.Code, ContextErrors.PageNameInvalid.Code };
        foreach (var error in PageRules.Check([pin], facts).Where(e => codes.Contains(e.Code, StringComparer.Ordinal)))
        {
            if (Location.Match(error.Message) is { Success: true } found)
            {
                var action = found.Groups["action"].Success ? int.Parse(found.Groups["action"].Value, CultureInfo.InvariantCulture) : -1;
                return Sentence(error.Code, action, found.Groups["detail"].Value, written);
            }
        }

        return null;
    }

    private static string Sentence(string code, int action, string detail, IReadOnlyList<PageListAction> written)
    {
        if (code == ContextErrors.PageIdDuplicate.Code)
        {
            return $"Not added: two actions are called {written[action].Name}";
        }

        if (code == ContextErrors.PageOverCap.Code)
        {
            return action < 0 ? $"Not added: {detail}" : $"Not added: action {Count(action + 1)} relates to {detail}";
        }

        var subject = action < 0 ? "the name" : $"action {Count(action + 1)}'s name";
        return char.IsDigit(detail[0]) ? $"Not added: {subject} is {detail}" : $"Not added: {subject} {detail}";
    }

    private static string Count(int n) => n.ToString(CultureInfo.InvariantCulture);

    [GeneratedRegex(@"index\.pages\[0\](?:\.actions\[(?<action>\d+)\])?(?:\.\w+)?: (?<detail>.+)$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.Singleline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Location { get; }
}

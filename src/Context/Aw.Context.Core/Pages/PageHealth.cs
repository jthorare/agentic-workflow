using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;

namespace Aw.Context.Core.Pages;

/// <summary>The doctor row 'context:pages': the approved profile's pages against the map as stored, without the owner overlay (pages spec §3.7).</summary>
public static class PageHealth
{
    /// <summary>The row's name.</summary>
    public const string Name = "context:pages";

    private const string ConceptPrefix = "concept:";
    private const string Fix = "sindri context page list --suggested";

    /// <summary>The row when the profile names no pages.</summary>
    public static MapHealth NoPages { get; } = new(Name, MapHealthState.Ok, "no pages named", Fix: null);

    /// <summary>The row when there is no map to check against.</summary>
    public static MapHealth NoMap { get; } = new(Name, MapHealthState.Ok, "no map yet", Fix: null);

    /// <summary>Checks the pages against the map.</summary>
    /// <param name="repos">The approved profile's repos, each with its pages.</param>
    /// <param name="stored">The map's graph before the deny cut.</param>
    /// <param name="visible">The same graph after the deny cut, without the pages laid over it.</param>
    /// <returns>Ok when every key and concept is on the map and the caps hold, else a warning naming the first problem and how many there are.</returns>
    public static MapHealth Check(IReadOnlyList<ContextRepo> repos, GraphSnapshot stored, GraphSnapshot visible)
    {
        ArgumentNullException.ThrowIfNull(repos);
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(visible);
        var pages = repos.SelectMany(r => r.Pages).DistinctBy(p => p.Id).ToList();
        if (pages.Count == 0)
        {
            return NoPages;
        }

        var problems = new List<string>();
        if (pages.Count > PageRules.MaxPages)
        {
            problems.Add(Text($"{pages.Count} pages; at most {PageRules.MaxPages}"));
        }

        var storedKeys = stored.Nodes.Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal);
        var shownKeys = visible.Nodes.Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal);
        foreach (var page in pages)
        {
            Page(page, (storedKeys, shownKeys), problems);
        }

        return problems.Count == 0
            ? new MapHealth(Name, MapHealthState.Ok, Text($"{pages.Count} {(pages.Count == 1 ? "page" : "pages")}, all on the map"), Fix: null)
            : new MapHealth(Name, MapHealthState.Warn, problems.Count == 1 ? problems[0] : Text($"{problems.Count} problems; first: {problems[0]}"), Fix);
    }

    private static void Page(PagePin page, (HashSet<string> Stored, HashSet<string> Shown) keys, List<string> problems)
    {
        if (page.SurfaceKey is { } surface && Missing(keys, surface) is { } surfaceWhy)
        {
            problems.Add($"page '{page.Id.Value}': surface {surface} {surfaceWhy}");
        }

        if (page.Actions.Count > PageRules.MaxActions)
        {
            problems.Add(Text($"page '{page.Id.Value}': {page.Actions.Count} actions; at most {PageRules.MaxActions}"));
        }

        foreach (var action in page.Actions)
        {
            Action(page.Id.Value, action, keys, problems);
        }
    }

    private static void Action(string page, PageActionPin action, (HashSet<string> Stored, HashSet<string> Shown) keys, List<string> problems)
    {
        if (action.ActionKey is { } actionKey && Missing(keys, actionKey) is { } why)
        {
            problems.Add($"page '{page}': action {actionKey} {why}");
        }

        if (action.Concepts.Count > PageRules.MaxConcepts)
        {
            problems.Add(Text($"page '{page}': action '{action.Id.Value}' has {action.Concepts.Count} concepts; at most {PageRules.MaxConcepts}"));
        }

        foreach (var concept in action.Concepts)
        {
            var conceptKey = concept.StartsWith(ConceptPrefix, StringComparison.Ordinal) ? concept : ConceptPrefix + concept;
            if (!Has(keys.Shown, conceptKey))
            {
                problems.Add($"page '{page}': action '{action.Id.Value}': concept {conceptKey} is gone from the map");
            }
        }
    }

    // Null when the key is on the map as the caller sees it; else whether a deny rule hides it or it is gone.
    private static string? Missing((HashSet<string> Stored, HashSet<string> Shown) keys, string key) =>
        Has(keys.Shown, key) ? null : Has(keys.Stored, key) ? "is hidden by a deny rule" : "is gone from the map";

    private static bool Has(HashSet<string> set, string key) => set.Contains(key) || set.Contains(EntryKey.Normalized(key));

    private static string Text(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}

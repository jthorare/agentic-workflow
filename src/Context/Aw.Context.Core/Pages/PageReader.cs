using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Results;
using Aw.Kernel;

namespace Aw.Context.Core.Pages;

/// <summary>Builds the pages answer from a graph the caller may see (pages spec §3.4): the owner's named pages and the suggested ones, each with its actions. Pure apart from the text policy it writes names with.</summary>
public static class PageReader
{
    /// <summary>Pages per answer when the question names none.</summary>
    public const int DefaultLimit = 50;

    /// <summary>The most pages one answer holds.</summary>
    public const int MaxLimit = 200;

    private const string Scheme = "page:";
    private const string ConceptPrefix = "concept:";
    private const string OwnerMark = "owner";
    private const string SuggestedMark = "suggested";

    /// <summary>One slice of the pages of a graph.</summary>
    /// <param name="query">The filter and the slice.</param>
    /// <param name="mapVersion">The map version read ('v13').</param>
    /// <param name="graph">The graph the caller may see, with the owner's pages laid over it.</param>
    /// <param name="repos">The profile's repos in profile order, each with its pages.</param>
    /// <param name="text">The caller's text policy.</param>
    /// <returns>The view, SND-CONTEXT-006 for a bad limit or offset, SND-CONTEXT-258 for a concept or repo the map lacks, or SND-CONTEXT-257 for a page it lacks.</returns>
    public static Result<PagesView> Build(PagesQuery query, string mapVersion, GraphSnapshot graph, IReadOnlyList<ContextRepo> repos, MapText text)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(mapVersion);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(repos);
        ArgumentNullException.ThrowIfNull(text);
        var slice = Slice(query);
        if (slice.IsError)
        {
            return slice.Error;
        }

        var index = new PageIndex(graph, repos);
        var all = index.Entries();
        var refused = Refuse(query, index, repos, all);
        if (refused is { } error)
        {
            return error;
        }

        var concept = query.Concept is { } c ? ConceptPrefix + c.Value : null;
        var matching = all.Where(e => Matches(query, concept, e)).ToList();
        var (offset, size) = slice.Value;
        var shown = matching.Skip(offset).Take(size).Select(e => Row(e, text)).ToList();
        return new PagesView(shown, matching.Count, offset + shown.Count < matching.Count, mapVersion);
    }

    /// <summary>How many pages (owner or suggested) express each concept, directly or through an action on them.</summary>
    /// <param name="graph">The graph the caller may see.</param>
    /// <returns>Concept key ('concept:scheduling') to its page count; concepts no page reaches are left out.</returns>
    public static IReadOnlyDictionary<string, int> PagesPerConcept(GraphSnapshot graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var entries = new PageIndex(graph, []).Entries();
        return entries.SelectMany(e => e.ConceptKeys.Distinct(StringComparer.Ordinal)).GroupBy(k => k, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
    }

    private static Result<(int Offset, int Size)> Slice(PagesQuery query)
    {
        if (query.Limit is < 1)
        {
            return ContextErrors.QueryInvalid.With("'limit' is at least 1 (fix: ask for one or more, or leave it out)");
        }

        if (query.Limit > MaxLimit)
        {
            return ContextErrors.QueryInvalid.With(string.Create(CultureInfo.InvariantCulture, $"'limit' is at most {MaxLimit} (fix: ask for {MaxLimit} or fewer, and page with offset)"));
        }

        return query.Offset is < 0
            ? ContextErrors.QueryInvalid.With("'offset' is at least 0 (fix: leave it out for the first page)")
            : (query.Offset ?? 0, query.Limit ?? DefaultLimit);
    }

    private static Error? Refuse(PagesQuery query, PageIndex index, IReadOnlyList<ContextRepo> repos, List<PageEntry> all)
    {
        if (query.Concept is { } concept && !index.HasConcept(ConceptPrefix + concept.Value))
        {
            return ContextErrors.PageQueryUnknown.With($"context_pages: no concept '{concept.Value}'{Fix(PageRules.NearestId(concept.Value, index.ConceptIds()))}");
        }

        if (query.Repo is { } repo && !repos.Any(r => r.Name.Value == repo.Value))
        {
            return ContextErrors.PageQueryUnknown.With($"context_pages: no repo '{repo.Value}'{Fix(PageRules.NearestId(repo.Value, repos.Select(r => r.Name.Value)))}");
        }

        return query.Page is { } page && !all.Exists(e => e.Id == page)
            ? ContextErrors.PageNotFound.With($"no page is named '{page}'{Fix(PageRules.NearestId(page, all.Select(e => e.Id)))}")
            : null;
    }

    private static string Fix(string? near) => near is null ? string.Empty : $" (fix: try {near})";

    private static bool Matches(PagesQuery query, string? concept, PageEntry entry) =>
        (query.Marks == PageMarks.All || query.Marks == PageMarks.Owner == entry.Owner)
        && (query.Page is null || entry.Id == query.Page)
        && (query.Repo is not { } repo || entry.Repos.Contains(repo.Value, StringComparer.Ordinal))
        && (concept is null || entry.ConceptKeys.Contains(concept, StringComparer.Ordinal));

    private static PageRow Row(PageEntry entry, MapText text) => new(
        PageId.From(entry.Id), entry.Node.Key, Label(entry.Name, entry.Node, text), entry.Repos.Count == 1 ? entry.Repos[0] : null, entry.Owner ? OwnerMark : SuggestedMark, entry.Code,
        [.. entry.Actions.Select(a => new PageActionRow(PageActionId.From(a.Id), a.Node.Key, Label(a.Name, a.Node, text), a.Owner ? OwnerMark : SuggestedMark, a.Code, a.Concepts))]);

    private static string Label(string name, GraphNode node, MapText text) => text.Label(name, TrustPolicy.ForNode(node, node.Provenance == Provenance.Human));

    private sealed record ActionEntry(string Id, GraphNode Node, string Name, bool Owner, bool Code, IReadOnlyList<string> Concepts);

    private sealed record PageHead(string Id, GraphNode Node, string Name, bool Owner, IReadOnlyList<string> Repos);

    private sealed record Shelf(List<ActionEntry> Shown, int Count, IReadOnlyList<string> ConceptKeys);

    private sealed record PageEntry(string Id, GraphNode Node, string Name, bool Owner, bool Code, IReadOnlyList<string> Repos, IReadOnlyList<ActionEntry> Actions, IReadOnlyList<string> ConceptKeys, int ActionCount);

    private sealed class PageIndex
    {
        private readonly GraphSnapshot _graph;
        private readonly List<(PageId Id, PagePin Pin)> _pins;
        private readonly Dictionary<string, GraphNode> _nodes;
        private readonly ILookup<string, GraphEdge> _implementsInto;
        private readonly ILookup<string, GraphEdge> _actionsOn;
        private readonly ILookup<string, string> _expresses;

        public PageIndex(GraphSnapshot graph, IReadOnlyList<ContextRepo> repos)
        {
            _graph = graph;
            _pins = [.. repos.SelectMany(r => r.Pages).DistinctBy(p => p.Id).Select(p => (p.Id, p))];
            _nodes = graph.Nodes.GroupBy(n => n.Key.Value, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            _implementsInto = graph.Edges.Where(e => e.Type == EdgeType.Implements).ToLookup(e => e.To.Value, StringComparer.Ordinal);
            _actionsOn = graph.Edges.Where(e => e.Type == EdgeType.On && e.From.Kind == NodeKind.Action).ToLookup(e => e.To.Value, StringComparer.Ordinal);
            _expresses = graph.Edges.Where(e => e is { Type: EdgeType.Expresses, To.Kind: NodeKind.Concept } && _nodes.ContainsKey(e.To.Value)).ToLookup(e => e.From.Value, e => e.To.Value, StringComparer.Ordinal);
        }

        public bool HasConcept(string key) => _nodes.TryGetValue(key, out var node) && node.Key.Kind == NodeKind.Concept;

        public IEnumerable<string> ConceptIds() => _nodes.Values.Where(n => n.Key.Kind == NodeKind.Concept).Select(n => n.Key.Name);

        // Owner pages in profile order, then suggested pages by action count (most first) and key; suggested ids are given in that order, after every owner id.
        public List<PageEntry> Entries()
        {
            var owner = new List<(string Id, string? Repo, GraphNode Node)>();
            var suggested = new List<GraphNode>();
            foreach (var node in _graph.Nodes.Where(n => n.Key.Kind == NodeKind.Surface))
            {
                if (OwnerRef(node) is { } mark)
                {
                    owner.Add((mark.Id, mark.Repo, node));
                }
                else if (PageKinds.IsPage(node, _graph.Edges))
                {
                    suggested.Add(node);
                }
            }

            var order = _pins.Select((p, i) => (p.Id.Value, i)).ToDictionary(x => x.Value, x => x.i, StringComparer.Ordinal);
            var entries = owner.OrderBy(o => order.GetValueOrDefault(o.Id, int.MaxValue)).ThenBy(o => o.Node.Key.Value, StringComparer.Ordinal).Select(o => OwnerEntry(o.Id, o.Repo, o.Node)).ToList();
            var taken = new HashSet<string>(entries.Select(e => e.Id), StringComparer.Ordinal);
            var byKey = suggested.ToDictionary(n => n.Key.Value, StringComparer.Ordinal);
            var offered = WholeMap.PageOrder.Of(suggested, _graph.Edges).Select(k => byKey[k]).Select(n => (Node: n, Name: PageNames.Offer(n)));
            entries.AddRange(offered.Select(s => SuggestedEntry(PageSlugs.Unique(PageSlugs.Of(s.Name), taken), s.Name, s.Node)));
            return entries;
        }

        private PageEntry OwnerEntry(string id, string? repo, GraphNode node)
        {
            var pin = _pins.Where(p => p.Id.Value == id).Select(p => p.Pin).FirstOrDefault();
            return Entry(new PageHead(id, node, node.Label, Owner: true, repo is null ? Repos(node.Key.Value) : [repo]), Actions(node, pin));
        }

        private PageEntry SuggestedEntry(string id, string name, GraphNode node) => Entry(new PageHead(id, node, name, Owner: false, Repos(node.Key.Value)), Actions(node, pin: null));

        private PageEntry Entry(PageHead head, Shelf actions)
        {
            var (id, node, name, owner, repos) = head;
            var concepts = _expresses[node.Key.Value].Concat(actions.ConceptKeys).Distinct(StringComparer.Ordinal).ToList();
            return new PageEntry(id, node, name, owner, _implementsInto[node.Key.Value].Any(), repos, actions.Shown, concepts, actions.Count);
        }

        private Shelf Actions(GraphNode page, PagePin? pin)
        {
            var nodes = _actionsOn[page.Key.Value].Select(e => e.From.Value).Distinct(StringComparer.Ordinal).Select(k => _nodes.GetValueOrDefault(k)).OfType<GraphNode>().ToList();
            var taken = new HashSet<string>(StringComparer.Ordinal);
            var owned = new List<(int Order, ActionEntry Entry)>();
            var rest = new List<GraphNode>();
            foreach (var node in nodes.OrderBy(n => n.Key.Value, StringComparer.Ordinal))
            {
                if (OwnerRef(node) is null)
                {
                    rest.Add(node);
                    continue;
                }

                var at = PinIndex(pin, node);
                var id = pin is not null && at >= 0 ? pin.Actions[at].Id.Value : null;
                owned.Add((at < 0 ? int.MaxValue : at, Action(PageSlugs.Unique(id ?? PageSlugs.Of(node.Label), taken), node, node.Label, owner: true)));
            }

            var ordered = owned.OrderBy(o => o.Order).Select(o => o.Entry)
                .Concat(rest.Select(n => Action(PageSlugs.Unique(PageSlugs.Of(n.Label), taken), n, n.Label, owner: false)));
            return new Shelf([.. ordered.Take(PageRules.MaxActions)], nodes.Count, [.. nodes.SelectMany(n => _expresses[n.Key.Value])]);
        }

        private static int PinIndex(PagePin? pin, GraphNode action)
        {
            if (pin is null)
            {
                return -1;
            }

            var prefix = $"action:owner/{pin.Id.Value}/";
            for (var i = 0; i < pin.Actions.Count; i++)
            {
                if ((pin.Actions[i].ActionKey is { } written && string.Equals(EntryKey.Normalized(written), EntryKey.Normalized(action.Key.Value), StringComparison.Ordinal)) || action.Key.Value == prefix + pin.Actions[i].Id.Value)
                {
                    return i;
                }
            }

            return -1;
        }

        private ActionEntry Action(string id, GraphNode node, string name, bool owner) =>
            new(id, node, name, owner, _implementsInto[node.Key.Value].Any(), [.. _expresses[node.Key.Value].Select(k => k[ConceptPrefix.Length..]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)]);

        private List<string> Repos(string key) => [.. _implementsInto[key].SelectMany(e => e.EvidenceOrEmpty).Select(RepoOf).OfType<string>().Distinct(StringComparer.Ordinal)];

        private static (string Id, string? Repo)? OwnerRef(GraphNode node)
        {
            foreach (var reference in node.EvidenceOrEmpty)
            {
                var value = reference.Key.Value;
                if (!value.StartsWith(Scheme, StringComparison.Ordinal))
                {
                    continue;
                }

                var rest = value[Scheme.Length..];
                var slash = rest.IndexOf('/', StringComparison.Ordinal);
                if (slash > 0 && PageId.TryFrom(rest[(slash + 1)..], out var id))
                {
                    return (id.Value, rest[..slash]);
                }
            }

            return null;
        }

        private static string? RepoOf(EvidenceRef reference)
        {
            const string prefix = "repo:";
            var value = reference.Key.Value;
            var slash = value.IndexOf('/', StringComparison.Ordinal);
            return value.StartsWith(prefix, StringComparison.Ordinal) && slash > 0 ? value[prefix.Length..slash] : null;
        }
    }
}

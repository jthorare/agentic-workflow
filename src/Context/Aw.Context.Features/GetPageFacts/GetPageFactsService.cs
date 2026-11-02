using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Pages;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetPageFacts;

/// <summary>The current map's concepts, and the repos of each surface and action key, for the page write check (pages spec §3.1). Reads as <c lang="text">CheckPagesService</c> does: the deny cut applies and the pages are not laid over the map, so a page can point only at what the map itself has.</summary>
internal sealed class GetPageFactsService(IGraphStore store) : IService<GetPageFactsRequest, PageMapFacts>
{
    public async ValueTask<Result<PageMapFacts>> Handle(GetPageFactsRequest request, CancellationToken cancellationToken)
    {
        var current = await store.Current(cancellationToken).ConfigureAwait(false);
        if (current.IsError)
        {
            return current.Error;
        }

        if (current.Value is not { } version)
        {
            return new PageMapFacts(MapVersion: null, Facts(new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal), new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal), []), []);
        }

        var graph = await store.Read(version.Number, cancellationToken).ConfigureAwait(false);
        var overrides = await store.Overrides(cancellationToken).ConfigureAwait(false);
        if (graph.IsError || overrides.IsError)
        {
            return graph.IsError ? graph.Error : overrides.Error;
        }

        // The write runs as the caller, so a session sees only its repo's part (callerScope on, as the pages read); no pages are laid over the map.
        var seen = MapAccess.View(graph.Value, overrides.Value, request.Scope with { OmitPages = true }, pending: false);
        if (seen.IsError)
        {
            return seen.Error;
        }

        var visible = seen.Value.Graph;
        var implementsInto = visible.Edges.Where(e => e.Type == EdgeType.Implements).ToLookup(e => e.To.Value, StringComparer.Ordinal);
        var concepts = visible.Nodes.Where(n => n.Key.Kind == NodeKind.Concept).Select(Concept).OfType<PageMapConcept>().ToList();
        var surfaces = ReposOf(visible, NodeKind.Surface, implementsInto);
        var actions = ReposOf(visible, NodeKind.Action, implementsInto);
        return new PageMapFacts(MapVersionRef.Tag(version.Number), Facts(surfaces, actions, concepts), concepts);
    }

    private static PageFacts Facts(Dictionary<string, IReadOnlyList<string>> surfaces, Dictionary<string, IReadOnlyList<string>> actions, List<PageMapConcept> concepts) =>
        new(concepts.Select(c => c.Id).ToHashSet(), surfaces, actions) { Stored = EntryKey.Normalized };

    private static PageMapConcept? Concept(GraphNode node) =>
        ConceptId.TryFrom(node.Key.Name, out var id) ? new PageMapConcept(id, node.Label) : null;

    // The repos of a key are those its 'implements' evidence names, as the pages read lists them.
    private static Dictionary<string, IReadOnlyList<string>> ReposOf(GraphSnapshot graph, NodeKind kind, ILookup<string, GraphEdge> implementsInto) =>
        graph.Nodes.Where(n => n.Key.Kind == kind).ToDictionary(
            n => n.Key.Value,
            n => (IReadOnlyList<string>)[.. implementsInto[n.Key.Value].SelectMany(e => e.EvidenceOrEmpty).Select(RepoOf).OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
            StringComparer.Ordinal);

    private static string? RepoOf(EvidenceRef reference)
    {
        const string prefix = "repo:";
        var value = reference.Key.Value;
        var slash = value.IndexOf('/', StringComparison.Ordinal);
        return value.StartsWith(prefix, StringComparison.Ordinal) && slash > 0 ? value[prefix.Length..slash] : null;
    }
}

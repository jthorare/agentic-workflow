using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Results;
using Aw.Context.Core.Walks;
using Aw.Kernel;

namespace Aw.Context.Core.Code;

/// <summary>Builds the graph half of the code lens (S-CL): pure over an applied graph, no repo read.</summary>
public static class CodeLensBuilder
{
    /// <summary>Builds the view without lexical data.</summary>
    /// <param name="basis">The map read and when.</param>
    /// <param name="graph">The graph a caller may see: overrides applied, a session's scope already cut.</param>
    /// <param name="anchors">The anchors (keys, aliases or labels).</param>
    /// <returns>The view; SND-CONTEXT-003 when none resolves, SND-CONTEXT-037 for an ambiguous name.</returns>
    public static Result<CodeView> Build(LensBasis basis, GraphSnapshot graph, IReadOnlyList<string> anchors) => Build(basis, graph, anchors, WalkOptions.Serial);

    /// <summary>Builds the view without lexical data, running the per-anchor walks over <paramref name="walk"/>'s cores; the answer is the same at any degree.</summary>
    /// <param name="basis">The map read and when.</param>
    /// <param name="graph">The graph a caller may see: overrides applied, a session's scope already cut.</param>
    /// <param name="anchors">The anchors (keys, aliases or labels).</param>
    /// <param name="walk">How many anchors' walks may run at once.</param>
    /// <returns>The view; SND-CONTEXT-003 when none resolves, SND-CONTEXT-037 for an ambiguous name.</returns>
    public static Result<CodeView> Build(LensBasis basis, GraphSnapshot graph, IReadOnlyList<string> anchors, WalkOptions walk)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(anchors);
        var resolved = AnchorSet.Resolve(graph, [.. anchors.Distinct(StringComparer.Ordinal)], depth: 0, options: null, walk);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        var (scopes, missing) = resolved.Value;
        var reach = new CodeReach(graph, scopes);
        var things = CodeThings.Of(reach);
        var (linkedFiles, linkedTests) = CodeFiles.Link(CodeFiles.Of(reach), CodeTests.Of(reach));
        var repoOf = NodeRepos.Index(graph, basis.MapRepo);
        var listedFiles = linkedFiles.Select(f => f with { Repo = repoOf.GetValueOrDefault("code:" + f.Path) }).ToList();
        var listedTests = linkedTests.Select(t => t with { Repo = repoOf.GetValueOrDefault("code:" + t.Path) }).ToList();
        var listedThings = things.ToList();
        return new CodeView(
            1,
            [.. scopes.Select(Anchor)],
            missing,
            basis.Map,
            basis.Draft,
            basis.AsOf,
            listedFiles,
            listedTests,
            new CodeData(listedThings, []),
            CodeSymbols.Unavailable,
            CodeSymbols.Unavailable,
            new CodeLensCounts(linkedFiles.Count, linkedTests.Count, things.Count, SchemaFiles: 0, linkedFiles.Count(f => f.TestedBy.Count == 0), Unlocated: 0),
            new CodeLensTruncated(Files: false, Tests: false, Things: false, SchemaFiles: false, SearchedFiles: false),
            [],
            CodeEdges.Of(graph, reach.Union, listedFiles, listedTests, listedThings),
            AnchorSet.Coverage(basis, graph, scopes, reach.Union, walk),
            Searched(basis, scopes, listedFiles.Count > 0 || listedTests.Count > 0 || listedThings.Count > 0),
            [],
            new Dictionary<string, Citation>(StringComparer.Ordinal));
    }

    // The code lens does not cascade: depth 0, no hops. The map and the code index are its graph-half sources; the lexical rows join in CodeLensMerge.
    private static LensSearched Searched(LensBasis basis, List<LensScope> scopes, bool codeFound)
    {
        var intent = scopes.TrueForAll(s => LensIntent.Records(s).Count == 0 && LensIntent.Decisions(s).Count == 0);
        var product = scopes.TrueForAll(s => LensProduct.Lists(s, 0) is { Own.Count: 0, Neighbors.Count: 0, Affects.Count: 0 });
        return new LensSearched(
            [.. scopes.Select(s => s.Anchor.Key.Value)],
            basis.Map,
            Depth: 0,
            [],
            [SearchedSources.Map(basis.MapCreated ?? basis.AsOf), SearchedSources.CodeIndex],
            new SearchedBands(new BandSearched(intent), new BandSearched(product), new BandSearched(!codeFound)));
    }

    /// <summary>The search terms of a data thing: the forms of its key name, its label and every alias that targets it.</summary>
    /// <param name="graph">The graph.</param>
    /// <param name="thing">The thing's key.</param>
    /// <returns>The forms; empty when the thing is not searchable.</returns>
    public static IReadOnlyList<string> ThingTerms(GraphSnapshot graph, NodeKey thing) => ThingTerms(graph, thing, static name => name);

    /// <summary>The search terms of a data thing, with each name cleaned first (the search must only carry text the answer could carry).</summary>
    /// <param name="graph">The graph.</param>
    /// <param name="thing">The thing's key.</param>
    /// <param name="clean">Cleans one name; a name it returns empty gives no terms.</param>
    /// <returns>The forms; empty when the thing is not searchable.</returns>
    public static IReadOnlyList<string> ThingTerms(GraphSnapshot graph, NodeKey thing, Func<string, string> clean)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(clean);
        var names = new List<string> { CodeTerms.KeyName(thing.Value) };
        if (graph.Nodes.FirstOrDefault(n => n.Key.Value == thing.Value) is { } node)
        {
            names.Add(node.Label);
        }

        names.AddRange(graph.Aliases.Where(a => a.Target.Value == thing.Value).Select(a => CodeTerms.KeyName(a.Alias.Value)));
        return CodeTerms.Forms(names.Select(clean));
    }

    private static LensAnchor Anchor(LensScope scope) =>
        new(scope.Anchor.Key, scope.Anchor.Label, scope.Anchor.Key.Kind, TrustPolicy.ForNode(scope.Anchor, LensScope.Confirmed(scope.Anchor.Provenance)));
}

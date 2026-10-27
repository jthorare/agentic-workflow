using Aw.Context.Core.Code;
using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Graph;
using Aw.Context.Core.History;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Core.ScopeGraph;
using Aw.Context.Core.Search;
using Aw.Context.Core.Subgraph;
using Aw.Context.Core.WholeMap;
using Aw.Context.Features.FindPath;
using Aw.Context.Features.GetFileMap;
using Aw.Context.Features.GetLenses;
using Aw.Context.Features.ListNeighbors;
using Aw.Context.Features.ShowNode;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features;

/// <summary>The graph reads of the facade and the app's lenses and file-map reads: each method dispatches to its slice (spec §4).</summary>
internal sealed class ContextGraph(
    IService<ShowNodeRequest, NodeView> show,
    IService<ListNeighborsRequest, NeighborsView> neighbors,
    IService<FindPathRequest, PathView> path,
    IService<GetLensesRequest, LensesView> lenses,
    IService<GetLensesSetRequest, LensesSetView> lensesSet,
    IService<GetFileMapRequest, FileMapView> fileMap,
    ContextCodeHistory codeHistory) : IContextGraph, IContextLenses, IContextCode, IContextHistory, IContextConcepts
{
    public ValueTask<Result<NodeView>> ShowNode(string key, ContextScope scope, CancellationToken cancellationToken) =>
        show.Handle(new ShowNodeRequest(key, scope), cancellationToken);

    public ValueTask<Result<NeighborsView>> Neighbors(string key, int? depth, ContextScope scope, CancellationToken cancellationToken) =>
        neighbors.Handle(new ListNeighborsRequest(key, depth, scope), cancellationToken);

    public ValueTask<Result<PathView>> Path(string from, string to, ContextScope scope, CancellationToken cancellationToken) =>
        path.Handle(new FindPathRequest(from, to, scope), cancellationToken);

    public ValueTask<Result<LensesView>> Lenses(string anchor, MapRead read, int? depth, ContextScope scope, CancellationToken cancellationToken) =>
        Lenses(anchor, read, new LensRange(depth), scope, cancellationToken);

    public ValueTask<Result<LensesView>> Lenses(string anchor, MapRead read, LensRange range, ContextScope scope, CancellationToken cancellationToken) =>
        lenses.Handle(new GetLensesRequest(anchor, read, range.Depth, scope) { Hops = range.Hops }, cancellationToken);

    public ValueTask<Result<LensesSetView>> LensesOf(IReadOnlyList<string> anchors, MapRead read, int? depth, ContextScope scope, CancellationToken cancellationToken) =>
        LensesOf(anchors, read, new LensRange(depth), scope, cancellationToken);

    public ValueTask<Result<LensesSetView>> LensesOf(IReadOnlyList<string> anchors, MapRead read, LensRange range, ContextScope scope, CancellationToken cancellationToken) =>
        lensesSet.Handle(new GetLensesSetRequest(anchors, read, range.Depth, scope) { Hops = range.Hops }, cancellationToken);

    public ValueTask<Result<SubgraphView>> Subgraph(SubgraphQuery query, ContextScope scope, CancellationToken cancellationToken) =>
        codeHistory.Subgraph(query, scope, cancellationToken);

    public ValueTask<Result<WholeMapView>> WholeMap(WholeMapQuery query, ContextScope scope, CancellationToken cancellationToken) =>
        codeHistory.WholeMap(query, scope, cancellationToken);

    public ValueTask<Result<MapSearchView>> Search(MapSearchQuery query, ContextScope scope, CancellationToken cancellationToken) =>
        codeHistory.Search(query, scope, cancellationToken);

    public ValueTask<Result<FileMapView>> FileMap(FileMapQuery query, ContextScope scope, CancellationToken cancellationToken) =>
        fileMap.Handle(new GetFileMapRequest(query, scope), cancellationToken);

    public ValueTask<Result<ScopeGraphView>> ScopeGraph(ScopeGraphQuery query, ContextScope scope, CancellationToken cancellationToken) =>
        codeHistory.ScopeGraph(query, scope, cancellationToken);

    public ValueTask<Result<bool>> RememberScopeGraph(string instanceAddress, ScopeGraphView view, CancellationToken cancellationToken) =>
        codeHistory.RememberScopeGraph(instanceAddress, view, cancellationToken);

    public ValueTask<Result<GraphRead>> ReadInScopeGraph(string instanceAddress, string runToken, GraphRead read, CancellationToken cancellationToken) =>
        codeHistory.ReadInScopeGraph(instanceAddress, runToken, read, cancellationToken);

    public ValueTask<Result<IReadOnlyList<GraphRead>>> GraphReads(string instanceAddress, string runToken, CancellationToken cancellationToken) =>
        codeHistory.GraphReads(instanceAddress, runToken, cancellationToken);

    public ValueTask<Result<CodeView>> Code(IReadOnlyList<string> anchors, bool draft, string? mapVersion, ContextScope scope, CancellationToken cancellationToken) =>
        codeHistory.Code(anchors, draft, mapVersion, scope, cancellationToken);

    public ValueTask<Result<HistoryView>> History(IReadOnlyList<string> anchors, HistoryWindow window, MapRead read, ContextScope scope, CancellationToken cancellationToken) =>
        codeHistory.History(anchors, window, read, scope, cancellationToken);

    public ValueTask<Result<ConceptsView>> Concepts(ConceptsQuery query, ContextScope scope, CancellationToken cancellationToken) =>
        codeHistory.Concepts(query, scope, cancellationToken);

    public ValueTask<Result<ConceptMembersView>> ConceptMembers(ConceptMembersQuery query, ContextScope scope, CancellationToken cancellationToken) =>
        codeHistory.ConceptMembers(query, scope, cancellationToken);

    public ValueTask<Result<ConceptGlossaryView>> ConceptGlossary(ConceptGlossaryQuery query, ContextScope scope, CancellationToken cancellationToken) =>
        codeHistory.ConceptGlossary(query, scope, cancellationToken);
}

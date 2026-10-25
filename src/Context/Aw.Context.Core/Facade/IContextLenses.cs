using Aw.Context.Core.Engine;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Core.Search;
using Aw.Context.Core.Subgraph;
using Aw.Context.Core.WholeMap;
using Aw.Kernel;

namespace Aw.Context.Core.Facade;

/// <summary>
/// The app's two reads (spec §8.8, §8.9, stage S-4): the lenses around one node and the file map of a repo path. A session
/// sees only its repo's scope. IContextEngine extends it.
/// </summary>
public interface IContextLenses
{
    /// <summary>The lenses around one node in the map <paramref name="read"/> names: the current version, the draft or a retained version (scoping spec C10).</summary>
    /// <param name="anchor">A node key, alias or label.</param>
    /// <param name="read">Which map.</param>
    /// <param name="depth">How far the neighbors range and the cascade runs (0 to 4), or null for the profile's 'context.cascadeDepth'.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The lenses, or SND-CONTEXT-001/003/004/006/020/030/037/044/120.</returns>
    public ValueTask<Result<LensesView>> Lenses(string anchor, MapRead read, int? depth, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>The union of the lenses around several nodes (scoping spec C1, schema 2), read from one map snapshot.</summary>
    /// <param name="anchors">Node keys, aliases or labels; duplicates count once.</param>
    /// <param name="read">Which map.</param>
    /// <param name="depth">How far the neighbors range and the cascade runs (0 to 4), or null for the profile's 'context.cascadeDepth'.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The lenses; an anchor this caller can't read goes to 'missingAnchors', and none resolving is SND-CONTEXT-003. Also SND-CONTEXT-001/004/006/020/030/037/044/120.</returns>
    public ValueTask<Result<LensesSetView>> LensesOf(IReadOnlyList<string> anchors, MapRead read, int? depth, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>The subgraph around a set of node keys: the bands, edges, conflicts and coverage of those nodes and what they reach within a depth (scoping spec C14).</summary>
    /// <param name="query">The keys, the depth and which map.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The view; a key this caller can't read goes to 'missing' (all missing is still an answer). SND-CONTEXT-001/004/006/020/030/037/044/120 otherwise.</returns>
    public ValueTask<Result<SubgraphView>> Subgraph(SubgraphQuery query, ContextScope scope, CancellationToken cancellationToken);
    /// <summary>The lenses around one node with the cascade hops the caller names (the opt-in 'component' hop; C4).</summary>
    /// <param name="anchor">A node key, alias or label.</param>
    /// <param name="read">Which map.</param>
    /// <param name="range">The depth and the cascade hops.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The lenses, or SND-CONTEXT-140 for a bad hops list, else as the overload without hops.</returns>
    public ValueTask<Result<LensesView>> Lenses(string anchor, MapRead read, LensRange range, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>The union of the lenses around several nodes with the cascade hops the caller names.</summary>
    /// <param name="anchors">Node keys, aliases or labels; duplicates count once.</param>
    /// <param name="read">Which map.</param>
    /// <param name="range">The depth and the cascade hops.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The lenses, or SND-CONTEXT-140 for a bad hops list, else as the overload without hops.</returns>
    public ValueTask<Result<LensesSetView>> LensesOf(IReadOnlyList<string> anchors, MapRead read, LensRange range, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>The whole map in one answer (Part D): every node and edge when the map fits the node budget, else one entry per area; a session sees only its repo.</summary>
    /// <param name="query">The detail, the keys to place, which map and whose repo.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The view, or SND-CONTEXT-001/003/004/006/020/030/044/045/120.</returns>
    public ValueTask<Result<WholeMapView>> WholeMap(WholeMapQuery query, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Ranked full-text search over everything in the map the caller may read: node labels, file paths, record titles, decision wording and quotes, descriptive attributes (title over path over text).</summary>
    /// <param name="query">The words, the limit, the area and repo, and which map.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The hits best first and how many matched in all (a session whose repo implements nothing gets none), or SND-CONTEXT-001/004/006/020/030/044/045/120.</returns>
    public ValueTask<Result<MapSearchView>> Search(MapSearchQuery query, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>The direct children of a repo path with their status.</summary>
    /// <param name="query">The repo, the path and the filters.</param>
    /// <param name="scope">The profile's context section and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The file map, or SND-CONTEXT-001/003/004/020/030/044/045 (045 for a repo outside the profile or a session's scope, answered the same way).</returns>
    public ValueTask<Result<FileMapView>> FileMap(FileMapQuery query, ContextScope scope, CancellationToken cancellationToken);
}

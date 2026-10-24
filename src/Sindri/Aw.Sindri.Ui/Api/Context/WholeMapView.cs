namespace Aw.Sindri.Ui.Api.Context;

/// <summary>
/// GET /sindri/context/map/whole (whole-map spec §2.1): the map at one of two levels of detail. Nodes and edges come with detail
/// "nodes" (or one opened area); the areas and the edges between them are always there, so the overview and a mini-map draw from any answer.
/// </summary>
/// <param name="SchemaVersion">The answer's schema.</param>
/// <param name="MapVersion">The map version read ('v3').</param>
/// <param name="MapCommit">The commit the map was built at, or null.</param>
/// <param name="Draft">True when the draft map was read.</param>
/// <param name="AsOf">When the map was built.</param>
/// <param name="Repo">The repo; null when the profile has several and none was asked for.</param>
/// <param name="Detail">nodes or areas.</param>
/// <param name="Fallback">over-budget when the answer is areas because the map is over the node budget; else null.</param>
/// <param name="Counts">The map's size.</param>
/// <param name="Areas">Every area, sorted by key.</param>
/// <param name="AreaEdges">The edges between areas, aggregated.</param>
/// <param name="Nodes">The nodes, with detail nodes or an opened area.</param>
/// <param name="Edges">The edges between the returned nodes.</param>
/// <param name="Area">The opened area.</param>
/// <param name="Located">Where the located keys are.</param>
/// <param name="Truncated">True when the answer was cut.</param>
/// <param name="Repos">The repos the map covers; null until Sindri serves it, which reads as one repo.</param>
internal sealed record WholeMapView(
    int SchemaVersion,
    string MapVersion,
    bool Draft,
    DateTimeOffset AsOf,
    string? Repo,
    string Detail,
    WholeCounts Counts,
    IReadOnlyList<WholeArea> Areas,
    IReadOnlyList<WholeAreaEdge> AreaEdges,
    bool Truncated,
    string? MapCommit = null,
    string? Fallback = null,
    IReadOnlyList<WholeNode>? Nodes = null,
    IReadOnlyList<WholeEdge>? Edges = null,
    WholeAreaInfo? Area = null,
    IReadOnlyList<WholeLocated>? Located = null,
    IReadOnlyList<WholeRepoSummary>? Repos = null);

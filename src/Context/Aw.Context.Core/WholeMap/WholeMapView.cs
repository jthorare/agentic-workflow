using System.Text.Json.Serialization;

namespace Aw.Context.Core.WholeMap;

/// <summary>'GET /sindri/context/map/whole' (Part D, §2.1): the whole map at one of two levels of detail. The areas and the edges between them are always there, so a page can draw the overview from any answer.</summary>
/// <param name="SchemaVersion">1.</param>
/// <param name="MapVersion">The map read ('v13').</param>
/// <param name="Draft">True when the draft was read.</param>
/// <param name="AsOf">When the map version was written.</param>
/// <param name="Repo">The repo this answer is for, or null when the profile has several and the question named none.</param>
/// <param name="Detail">'nodes' or 'areas', what the answer carries.</param>
/// <param name="Counts">What the map holds.</param>
/// <param name="Areas">Every area, sorted by key.</param>
/// <param name="AreaEdges">The edges between areas, aggregated by source, target and type.</param>
/// <param name="Truncated">True when a list was cut at its cap.</param>
public sealed record WholeMapView(
    [property: JsonPropertyOrder(1)] int SchemaVersion,
    [property: JsonPropertyOrder(2)] string MapVersion,
    [property: JsonPropertyOrder(4)] bool Draft,
    [property: JsonPropertyOrder(5)] DateTimeOffset AsOf,
    [property: JsonPropertyOrder(6)] string? Repo,
    [property: JsonPropertyOrder(7)] WholeMapDetail Detail,
    [property: JsonPropertyOrder(9)] WholeMapCounts Counts,
    [property: JsonPropertyOrder(10)] IReadOnlyList<WholeMapArea> Areas,
    [property: JsonPropertyOrder(11)] IReadOnlyList<WholeMapAreaEdge> AreaEdges,
    [property: JsonPropertyOrder(16)] bool Truncated)
{
    /// <summary>The commit the map was built at (first seven characters), or null for an imported map; left out when null.</summary>
    [JsonPropertyOrder(3)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MapCommit { get; init; }

    /// <summary>'over-budget' when areas answer a question that wanted nodes, else null; left out when null.</summary>
    [JsonPropertyOrder(8)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Fallback { get; init; }

    /// <summary>Every node (detail 'nodes') or the opened area's nodes, else null; left out when null.</summary>
    [JsonPropertyOrder(12)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<WholeMapNode>? Nodes { get; init; }

    /// <summary>Every edge between the returned nodes, else null; left out when null.</summary>
    [JsonPropertyOrder(13)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<WholeMapEdge>? Edges { get; init; }

    /// <summary>The opened area, or null; left out when null.</summary>
    [JsonPropertyOrder(14)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WholeMapAreaInfo? Area { get; init; }

    /// <summary>The area of each asked-for key, or null when none was asked; left out when null.</summary>
    [JsonPropertyOrder(15)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<WholeMapLocated>? Located { get; init; }
}

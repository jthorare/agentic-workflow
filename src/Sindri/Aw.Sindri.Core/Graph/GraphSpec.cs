using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Graph;

/// <summary>A workflow as a JSON document (issue 2258, Part 2 "The spec"): what a revision stores and what 'sindri wf validate' reads.</summary>
/// <param name="Version">The spec format's version, 1.</param>
/// <param name="Name">The workflow's name for people.</param>
/// <param name="Description">What it is for, or null.</param>
/// <param name="Concurrency">How its runs overlap, such as 'one-per-workflow', or null for the default.</param>
/// <param name="Resources">Its resources, or null.</param>
/// <param name="Nodes">The nodes, in the order the author wrote them (the order findings and cycle paths follow).</param>
/// <param name="Edges">The edges, in the order the author wrote them.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GraphSpec(
    int Version,
    string Name,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Description,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Concurrency,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GraphResources? Resources,
    IReadOnlyList<GraphNode> Nodes,
    IReadOnlyList<GraphEdge> Edges);

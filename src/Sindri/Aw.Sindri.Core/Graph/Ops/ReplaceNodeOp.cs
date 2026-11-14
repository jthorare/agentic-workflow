using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Ops;

/// <summary>
/// Puts another block in a node's slot (Workflows page spec E3): the node keeps its id and its outgoing edges. Shape only for now: applying it is refused with SND-GRAPH-119
/// until the engine lane builds it; then only a trigger can be replaced and any other kind is SND-GRAPH-112.
/// </summary>
/// <param name="Node">The node to replace.</param>
/// <param name="Type">The new block type, such as 'trigger_github'.</param>
/// <param name="Title">What the canvas shows, or null to keep the node's title.</param>
/// <param name="Config">The new block's config; null means an empty object.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReplaceNodeOp(
    [property: JsonRequired] NodeId Node,
    [property: JsonRequired] string Type,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Title = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JsonObject? Config = null) : GraphOp;

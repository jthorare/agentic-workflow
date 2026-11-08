using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph;

/// <summary>One node of a workflow graph: a block of <paramref name="Type"/> with its config.</summary>
/// <param name="Id">The node's id, the name references use.</param>
/// <param name="Type">The block type, such as 'schedule' or 'run_agent'.</param>
/// <param name="Title">What the canvas shows, or null.</param>
/// <param name="Config">The block's config; its strings may hold '{{node.field}}' references.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GraphNode(NodeId Id, string Type, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Title, JsonObject Config);

using System.Text.Json.Serialization;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph;

/// <summary>An edge from one node's output to another node.</summary>
/// <param name="From">The node it leaves.</param>
/// <param name="Output">The output it leaves by ('out' when null; a branching block names its own, such as 'true').</param>
/// <param name="To">The node it enters.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GraphEdge(NodeId From, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Output, NodeId To);

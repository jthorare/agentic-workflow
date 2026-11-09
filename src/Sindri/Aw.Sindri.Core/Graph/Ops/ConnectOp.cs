using System.Text.Json.Serialization;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Ops;

/// <summary>Adds an edge from one node's output to another node; the same edge twice is refused.</summary>
/// <param name="From">The node the edge leaves.</param>
/// <param name="To">The node the edge enters.</param>
/// <param name="Output">The branch it leaves by, such as 'true'; null means the default 'out'.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConnectOp(
    [property: JsonRequired] NodeId From,
    [property: JsonRequired] NodeId To,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Output = null) : GraphOp;

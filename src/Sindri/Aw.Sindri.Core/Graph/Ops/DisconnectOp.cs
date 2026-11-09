using System.Text.Json.Serialization;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Ops;

/// <summary>Removes one edge; an edge that is not there is refused.</summary>
/// <param name="From">The node the edge leaves.</param>
/// <param name="To">The node the edge enters.</param>
/// <param name="Output">The branch it leaves by; null means the default 'out'.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DisconnectOp(
    [property: JsonRequired] NodeId From,
    [property: JsonRequired] NodeId To,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Output = null) : GraphOp;

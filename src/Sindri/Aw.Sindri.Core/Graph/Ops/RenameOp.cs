using System.Text.Json.Serialization;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Ops;

/// <summary>Gives a node a new id and follows it: edges and '{{old.field}}' references in configs change with it.</summary>
/// <param name="Node">The node's current id.</param>
/// <param name="To">The new id; one that is already used is refused.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RenameOp(
    [property: JsonRequired] NodeId Node,
    [property: JsonRequired] NodeId To) : GraphOp;

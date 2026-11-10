using System.Text.Json.Serialization;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Ops;

/// <summary>Removes a node and every edge that touches it.</summary>
/// <param name="Node">The node to remove.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RemoveNodeOp([property: JsonRequired] NodeId Node) : GraphOp;

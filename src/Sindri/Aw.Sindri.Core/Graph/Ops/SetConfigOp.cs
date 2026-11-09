using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Ops;

/// <summary>Sets one value inside a node's config by dotted path ('output.properties.summary.type'); missing objects on the way are created, and a JSON null value removes the key.</summary>
/// <param name="Node">The node to change.</param>
/// <param name="Path">Dot-separated keys; a number picks an item of a list.</param>
/// <param name="Value">The new value, or null to remove the key.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SetConfigOp(
    [property: JsonRequired] NodeId Node,
    [property: JsonRequired] string Path,
    [property: JsonRequired] JsonNode? Value) : GraphOp;

using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Ops;

/// <summary>Adds a node at the end of the node list; an id that is already used is refused.</summary>
/// <param name="Id">The new node's id.</param>
/// <param name="Type">The block type, such as 'notify'.</param>
/// <param name="Title">What the canvas shows, or null.</param>
/// <param name="Config">The block's config; null means an empty object.</param>
/// <param name="OnFailure">'stop' or 'continue', or null for the default.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AddNodeOp(
    [property: JsonRequired] NodeId Id,
    [property: JsonRequired] string Type,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Title = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JsonObject? Config = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? OnFailure = null) : GraphOp;

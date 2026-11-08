using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Graph;

/// <summary>The default sandbox size of a workflow's agent blocks; a block's own 'sandbox' wins.</summary>
/// <param name="Memory">Memory, such as '4g', or null.</param>
/// <param name="Wall">Wall-clock limit, such as '45m', or null.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GraphSandbox(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Memory = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Wall = null);

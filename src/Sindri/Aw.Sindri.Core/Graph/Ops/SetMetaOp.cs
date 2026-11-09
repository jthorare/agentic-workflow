using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Graph.Ops;

/// <summary>Changes the workflow's own fields. A member left out stays; for description and concurrency an empty string clears the value.</summary>
/// <param name="Name">The new name, or null to keep it.</param>
/// <param name="Description">The new description, '' to clear it, or null to keep it.</param>
/// <param name="Concurrency">The new concurrency, '' to clear it, or null to keep it.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SetMetaOp(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Name = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Description = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Concurrency = null) : GraphOp;

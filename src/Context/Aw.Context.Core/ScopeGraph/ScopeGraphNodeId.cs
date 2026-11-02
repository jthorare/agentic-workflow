using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Context.Core.ScopeGraph;

/// <summary>
/// A scoping graph node's id: the map's own key (<c lang="text">feature:…</c>), a record id, <c lang="text">code:path#symbol</c> for a symbol or <c lang="text">concept:&lt;slug&gt;</c> for a concept. Unlike a
/// <see cref="Graph.NodeKey"/> it is not limited to the map's kinds, so it only has to be non-empty.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<ScopeGraphNodeId>))]
public readonly partial struct ScopeGraphNodeId : ITypedId<ScopeGraphNodeId, string>
{
    private static Validation Validate(string value) =>
        value.Length > 0 ? Validation.Ok : Validation.Invalid("A scoping graph node id is not empty");
}

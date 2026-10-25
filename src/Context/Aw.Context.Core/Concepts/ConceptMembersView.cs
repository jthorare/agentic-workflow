using System.Text.Json.Serialization;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Concepts;

/// <summary>'GET /sindri/context/concepts/{id}/members': one concept's members, one page at a time.</summary>
/// <param name="ApiVersion">1.</param>
/// <param name="Repo">The repo this answer is for, or null.</param>
/// <param name="MapVersion">The map read ('v13').</param>
/// <param name="Concept">The concept's key.</param>
/// <param name="Members">This page's members, by kind and key.</param>
/// <param name="Next">The cursor of the next page, or null on the last; left out when null.</param>
public sealed record ConceptMembersView(
    int ApiVersion,
    string? Repo,
    string MapVersion,
    NodeKey Concept,
    IReadOnlyList<ConceptMember> Members,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Next);

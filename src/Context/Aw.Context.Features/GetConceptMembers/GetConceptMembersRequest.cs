using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;

namespace Aw.Context.Features.GetConceptMembers;

/// <summary>One page of one concept's members.</summary>
/// <param name="Query">The concept, which map, whose repo and the page.</param>
/// <param name="Scope">The profile's context section and repos, and the caller.</param>
internal sealed record GetConceptMembersRequest(ConceptMembersQuery Query, ContextScope Scope);

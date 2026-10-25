using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;

namespace Aw.Context.Features.GetConcepts;

/// <summary>One page of the product's concepts.</summary>
/// <param name="Query">Which map, whose repo, the filter and the page.</param>
/// <param name="Scope">The profile's context section and repos, and the caller.</param>
internal sealed record GetConceptsRequest(ConceptsQuery Query, ContextScope Scope);

using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;

namespace Aw.Context.Features.GetConceptGlossary;

/// <summary>The glossary of the concepts of a map.</summary>
/// <param name="Query">Which map and whose repo.</param>
/// <param name="Scope">The profile's context section and repos, and the caller.</param>
internal sealed record GetConceptGlossaryRequest(ConceptGlossaryQuery Query, ContextScope Scope);

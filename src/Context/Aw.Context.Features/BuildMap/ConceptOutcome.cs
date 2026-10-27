using Aw.Context.Core.Graph;

namespace Aw.Context.Features.BuildMap;

/// <summary>The joined graph with its concepts, and the degraded notes the concept pass leaves.</summary>
/// <param name="Graph">The graph with the derived concepts.</param>
/// <param name="Notes">'concepts' when the namer could not answer, 'glossary' when the symbol vectors could not be read.</param>
internal sealed record ConceptOutcome(GraphSnapshot Graph, IReadOnlyList<string> Notes);

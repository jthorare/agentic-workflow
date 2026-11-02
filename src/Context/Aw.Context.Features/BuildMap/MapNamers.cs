using Aw.Context.Core.Extract;

namespace Aw.Context.Features.BuildMap;

/// <summary>The three passes of a map build that call a model, one collaborator: the cluster labeler, the concept step (which names the concepts) and the product-surface step.</summary>
/// <param name="Labeler">Names the clusters.</param>
/// <param name="Concepts">Derives and names the concepts.</param>
/// <param name="Surfaces">Decides which action lookalikes are one node, or none.</param>
internal sealed record MapNamers(ILabeler Labeler, ConceptPass Concepts, SurfacePickPass Surfaces);

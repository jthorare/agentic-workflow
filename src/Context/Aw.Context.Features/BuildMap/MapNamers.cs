using Aw.Context.Core.Extract;

namespace Aw.Context.Features.BuildMap;

/// <summary>The two passes of a map build that call a model, one collaborator: the cluster labeler and the concept step (which names the concepts).</summary>
/// <param name="Labeler">Names the clusters.</param>
/// <param name="Concepts">Derives and names the concepts.</param>
internal sealed record MapNamers(ILabeler Labeler, ConceptPass Concepts);

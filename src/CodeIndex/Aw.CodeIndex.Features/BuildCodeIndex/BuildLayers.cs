namespace Aw.CodeIndex.Features.BuildCodeIndex;

/// <summary>The layers a build runs after the structure and the syntax edges (spec §5.7); the clones and deps layers join them later.</summary>
/// <param name="Embeddings">The embeddings layer.</param>
/// <param name="Graph">The graph layer.</param>
public sealed record BuildLayers(EmbeddingsLayer Embeddings, GraphLayer Graph);

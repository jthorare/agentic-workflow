namespace Aw.CodeIndex.Core.Facade;

/// <summary>The answer to SymbolVectors: stored vectors only, nothing was embedded.</summary>
/// <param name="Stamp">Which index answered; its layer is embeddings.</param>
/// <param name="Model">The embedding model every vector came from.</param>
/// <param name="Vectors">The embedded symbols of the asked files, by path then line.</param>
/// <param name="Missing">The asked paths with no embedded symbol (not indexed, no symbols, or not embedded yet).</param>
public sealed record SymbolVectorsView(AnswerStamp Stamp, string Model, IReadOnlyList<SymbolVector> Vectors, IReadOnlyList<string> Missing);

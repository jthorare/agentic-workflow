using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Glossary;

/// <summary>A feature with the embeddings of the code symbols that implement it.</summary>
/// <param name="Feature">The feature's node key.</param>
/// <param name="Label">The feature's label, a source of terms.</param>
/// <param name="Symbols">The symbols' names and vectors; empty when the code index has none for this feature.</param>
public sealed record FeatureVectors(NodeKey Feature, string Label, IReadOnlyList<SymbolVector> Symbols);

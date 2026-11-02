using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Glossary;

/// <summary>Features whose symbol embeddings sit close together, with the words that set them apart.</summary>
/// <param name="Features">The member features, in key order.</param>
/// <param name="Terms">The cluster's top terms, best first.</param>
public sealed record GlossaryCluster(IReadOnlyList<NodeKey> Features, IReadOnlyList<string> Terms);

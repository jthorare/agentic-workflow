using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Glossary;

/// <summary>The glossary signal of a map: clusters of related features and the terms that characterize them.</summary>
/// <param name="Terms">Every cluster term, merged across clusters, by weight then term.</param>
/// <param name="Clusters">The clusters, ordered by their first feature's key.</param>
/// <param name="Unvectored">The features left out because they had no usable vectors.</param>
/// <param name="Truncated">True when the input passed the feature cap and only the first features, in key order, were used.</param>
public sealed record MapGlossary(
    IReadOnlyList<GlossaryTerm> Terms,
    IReadOnlyList<GlossaryCluster> Clusters,
    IReadOnlyList<NodeKey> Unvectored,
    bool Truncated = false);

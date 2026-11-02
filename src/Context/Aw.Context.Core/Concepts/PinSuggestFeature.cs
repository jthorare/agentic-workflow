using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Concepts;

/// <summary>One feature a search found.</summary>
/// <param name="Key">The feature key.</param>
/// <param name="Label">Its label, cleaned like every label.</param>
public sealed record PinSuggestFeature(NodeKey Key, string Label);

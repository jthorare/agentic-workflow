namespace Aw.Context.Core.Concepts;

/// <summary>The names a model gave the derived concepts, by the key of each concept's seed feature (never by its slug, so a rename keeps the id).</summary>
/// <param name="BySeed">A name per seed feature key.</param>
public sealed record ConceptNames(IReadOnlyDictionary<string, ConceptName> BySeed)
{
    /// <summary>No names: every concept takes its deterministic fallback.</summary>
    public static ConceptNames None { get; } = new(new Dictionary<string, ConceptName>(StringComparer.Ordinal));
}

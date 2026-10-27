namespace Aw.Context.Core.Concepts;

/// <summary>The names a model gave the derived concepts, by the key of each concept's seed feature (never by its slug, so a rename keeps the id).</summary>
/// <param name="BySeed">A name per seed feature key.</param>
/// <param name="Unavailable">Why naming was skipped (over the labeling cap, or the model's error), already prefixed with its error code; the build says it as a note. Null when the namer ran.</param>
public sealed record ConceptNames(IReadOnlyDictionary<string, ConceptName> BySeed, string? Unavailable = null)
{
    /// <summary>No names: every concept takes its deterministic fallback.</summary>
    public static ConceptNames None { get; } = new(new Dictionary<string, ConceptName>(StringComparer.Ordinal));

    /// <summary>No names because naming was skipped: the concepts take their deterministic names and the build says why.</summary>
    /// <param name="reason">The reason, prefixed with its error code.</param>
    public static ConceptNames Skipped(string reason) => None with { Unavailable = reason };
}

namespace Aw.Context.Core.Sources;

/// <summary>What a source is (spec §5.1 Describe).</summary>
/// <param name="Id">Its id.</param>
/// <param name="Kind">The layer it feeds.</param>
/// <param name="Freshness">Live or snapshot.</param>
/// <param name="SupportsAsOf">True when it can cut its answers to a past time; otherwise as-of hits come back leaky.</param>
public sealed record SourceDescriptor(SourceId Id, SourceKind Kind, SourceFreshness Freshness, bool SupportsAsOf)
{
    /// <summary>Where the source runs (README X61); <see cref="SourcePlacement.Local"/> unless it says otherwise.</summary>
    public SourcePlacement Placement { get; init; } = SourcePlacement.Local;

    /// <summary>The limits it declares (README X61), or null for none, as every local S-1A source does.</summary>
    public SourceLimits? Limits { get; init; }

    /// <summary>True when the source applies <see cref="SourceQuery.Pin"/>; one that doesn't is left out of a <c xml:lang="en">concept=</c> query, with a note, rather than returning unscoped hits.</summary>
    public bool HonorsPin { get; init; }
}

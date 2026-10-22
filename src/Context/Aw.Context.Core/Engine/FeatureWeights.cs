namespace Aw.Context.Core.Engine;

/// <summary>The weight of each ranker feature ('context.ranker.weights', spec §8.2 step 5); they start equal, at 1. A weight of 0 turns its feature off.</summary>
public sealed record FeatureWeights
{
    /// <summary>The widest weight the profile may ask for.</summary>
    public const double MaxWeight = 10;

    /// <summary>Records near an anchor rank higher.</summary>
    public double CascadeDistance { get; init; } = 1;

    /// <summary>Records that decide a node rank above ones that only mention it.</summary>
    public double EdgeType { get; init; } = 1;

    /// <summary>Human links rank above deterministic ones, and those above a model's.</summary>
    public double Provenance { get; init; } = 1;

    /// <summary>A record whose decision a later one replaced ranks lower.</summary>
    public double Supersession { get; init; } = 1;

    /// <summary>A recently linked record ranks higher.</summary>
    public double Recency { get; init; } = 1;
}

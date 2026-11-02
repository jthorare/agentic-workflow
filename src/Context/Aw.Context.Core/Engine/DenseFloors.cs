namespace Aw.Context.Core.Engine;

/// <summary>'context.index.floors': everything within <see cref="KeepDistance"/> is kept, else the nearest <see cref="RescueHits"/> within <see cref="RescueDistance"/>, else nothing (owned index spec §6).</summary>
public sealed class DenseFloors
{
    /// <summary>Cosine distance within which every hit is kept (default 0.5).</summary>
    public double KeepDistance { get; init; } = 0.5;

    /// <summary>Cosine distance within which the nearest hits are rescued when none is within <see cref="KeepDistance"/> (default 0.75).</summary>
    public double RescueDistance { get; init; } = 0.75;

    /// <summary>How many hits are rescued (default 8).</summary>
    public int RescueHits { get; init; } = 8;
}

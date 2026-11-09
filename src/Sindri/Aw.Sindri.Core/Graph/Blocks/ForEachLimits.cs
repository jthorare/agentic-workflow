namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>What a 'for_each' takes when its node does not say: the item cap and the batch size (profile 'workflow.forEach.maxItems' and 'workflow.forEach.batchSize').</summary>
/// <param name="MaxItems">The most items one 'for_each' takes.</param>
/// <param name="BatchSize">How many items one child workflow runs.</param>
public sealed record ForEachLimits(int MaxItems, int BatchSize)
{
    /// <summary>The item cap when nothing sets it.</summary>
    public const int DefaultMaxItems = 200;

    /// <summary>The largest cap a node or the profile may ask for.</summary>
    public const int MaxItemsCeiling = 10_000;

    /// <summary>The batch size when nothing sets it.</summary>
    public const int DefaultBatchSize = 20;

    /// <summary>The largest batch.</summary>
    public const int BatchSizeCeiling = 100;

    /// <summary>The defaults.</summary>
    public static ForEachLimits Defaults { get; } = new(DefaultMaxItems, DefaultBatchSize);
}

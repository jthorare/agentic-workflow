namespace Aw.Context.Core.Review;

/// <summary>The cleanup agent's fixed lines (owner F1, 2026-10-19).</summary>
public static class CleanupPolicy
{
    /// <summary>A model entry at or above this confidence is kept without asking; below it the agent judges it.</summary>
    public const double KeepAt = 0.75;

    /// <summary>The most entries in one question to the agent.</summary>
    public const int Batch = 40;

    /// <summary>The most tokens one cleanup pass may spend; the entries left when it is reached are removed.</summary>
    public const int MaxTokens = 100_000;
}

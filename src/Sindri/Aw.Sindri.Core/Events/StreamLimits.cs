namespace Aw.Sindri.Core.Events;

/// <summary>The event stream's limit (API spec §5.4): 64 open streams, the same for every caller.</summary>
public static class StreamLimits
{
    /// <summary>The most streams open at once.</summary>
    public const int Total = 64;

    /// <summary>True when one more stream fits: fewer than <see cref="Total"/> are open.</summary>
    /// <param name="open">The streams open now, every caller.</param>
    /// <returns>Whether it is admitted.</returns>
    public static bool Admits(int open) => open < Total;
}

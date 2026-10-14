using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Core.Events;

/// <summary>The event stream's limits (API spec §5.4): 16 open streams, 4 of them held for the human.</summary>
public static class StreamLimits
{
    /// <summary>The most streams open at once.</summary>
    public const int Total = 16;

    /// <summary>The streams only a human may open.</summary>
    public const int HumanReserved = 4;

    /// <summary>The most streams sessions may hold together.</summary>
    public const int Session = Total - HumanReserved;

    /// <summary>True when one more stream for <paramref name="caller"/> fits: a human while fewer than 16 are open; a session while sessions hold fewer than 12 and fewer than 16 are open.</summary>
    /// <param name="caller">Who asks.</param>
    /// <param name="open">The streams open now, every caller.</param>
    /// <param name="openSessions">The streams sessions hold now.</param>
    /// <returns>Whether it is admitted.</returns>
    public static bool Admits(CallerClass caller, int open, int openSessions)
        => throw new NotSupportedException("StreamLimits.Admits: P5 unit E2 makes the stream limit tests pass");
}

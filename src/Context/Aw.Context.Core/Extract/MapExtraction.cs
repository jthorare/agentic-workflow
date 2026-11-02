namespace Aw.Context.Core.Extract;

/// <summary>What <see cref="MapExtractor"/> found, and whether a profile recognizer ran out of time on a file (reported as degraded 'recognizers', never a failure).</summary>
/// <param name="Map">The nodes and edges.</param>
/// <param name="TimedOut">True when a profile recognizer's match timed out on some file.</param>
public sealed record MapExtraction(ExtractedMap Map, bool TimedOut)
{
    /// <summary>The journey steps and files the flow pass left out, by reason.</summary>
    public JourneySkips Skips { get; init; } = JourneySkips.None;
}

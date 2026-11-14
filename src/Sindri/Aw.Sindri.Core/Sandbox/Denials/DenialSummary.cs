namespace Aw.Sindri.Core.Sandbox.Denials;

/// <summary>One short line on what the sandbox refused in a launch (#3565), for a failure text that would otherwise hide it.</summary>
public static class DenialSummary
{
    /// <summary>The line: 'the sandbox refused N actions (target xCount, target, +K more)'.</summary>
    /// <param name="files">The launch's denial counters.</param>
    /// <returns>
    /// The line, or null when nothing was refused. N counts every refusal, overflow included. Entries group by target across files, most refused first, then by target; the top three show,
    /// ' xC' only when C is above 1. K is the distinct targets not shown plus one per overflow row (a kind whose targets the counter stopped naming).
    /// </returns>
    public static string? Line(IReadOnlyList<DenialFile> files) => throw new NotSupportedException();
}

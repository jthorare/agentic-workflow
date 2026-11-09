using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>A length of time written as a block config says it: '90s', '15m', '2h', '1d' or a run of them such as '1h30m'.</summary>
public static class BlockDuration
{
    /// <summary>Reads a duration.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The duration, longer than zero, or the reason the text is not one.</returns>
    public static Result<TimeSpan> Parse(string text) => throw new NotSupportedException();
}

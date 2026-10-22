using System.Text.RegularExpressions;

namespace Aw.Sindri.Core.Setup;

/// <summary>Reads how far a map build is from its progress lines ('extracting 40/100 files').</summary>
public static partial class MapBuildProgress
{
    /// <summary>The count a progress line states, as 'n/N'.</summary>
    /// <param name="line">One progress line.</param>
    /// <returns>The progress, or null when the line states no count (or one that can't be right).</returns>
    public static SetupProgress? Parse(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return Shape.Match(line) is { Success: true } m
            && int.TryParse(m.Groups["done"].ValueSpan, out var done) && int.TryParse(m.Groups["total"].ValueSpan, out var total) && total > 0 && done <= total
            ? new SetupProgress(done, total, "files")
            : null;
    }

    [GeneratedRegex(@"(?<![\d/])(?<done>\d{1,9})\s*/\s*(?<total>\d{1,9})(?![\d/])", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}

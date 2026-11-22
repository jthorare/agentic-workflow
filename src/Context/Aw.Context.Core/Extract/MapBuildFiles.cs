using System.Globalization;
using System.Text.RegularExpressions;

namespace Aw.Context.Core.Extract;

/// <summary>
/// How far a map build has read one repo's files, as its progress line says it ("extracting 150/412 files"). The engine writes the line and Sindri reads it back for
/// the whole map's 'building' answer, so both sides go through this one type and the text can't drift. The count is per repo: a build of two repos starts again at 0 for the second.
/// </summary>
/// <param name="Done">The files read so far.</param>
/// <param name="Total">The files the build reads in this repo.</param>
public sealed partial record MapBuildFiles(int Done, int Total)
{
    /// <summary>The progress line: "extracting &lt;done&gt;/&lt;total&gt; files".</summary>
    public string Line => string.Create(CultureInfo.InvariantCulture, $"extracting {Done}/{Total} files");

    /// <summary>The counts a progress line carries, or null when it is not a files line (or its counts make no sense: done above total).</summary>
    /// <param name="line">A job progress line.</param>
    /// <returns>The counts, or null.</returns>
    public static MapBuildFiles? From(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var match = FilesLine.Match(line);
        if (!match.Success)
        {
            return null;
        }

        var done = int.Parse(match.Groups["done"].ValueSpan, CultureInfo.InvariantCulture);
        var total = int.Parse(match.Groups["total"].ValueSpan, CultureInfo.InvariantCulture);
        return done <= total ? new MapBuildFiles(done, total) : null;
    }

    /// <summary>
    /// The counts of the newest files line, or null when no line is one. Later lines (labeling, heartbeats, other counts such as "clusters 3/9") say nothing about files, so
    /// they don't hide the files line before them.
    /// </summary>
    /// <param name="lines">A job's progress lines, oldest first.</param>
    /// <returns>The counts, or null.</returns>
    public static MapBuildFiles? Newest(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        MapBuildFiles? newest = null;
        foreach (var line in lines)
        {
            newest = From(line) ?? newest;
        }

        return newest;
    }

    // Nine digits at most, so the parse can't overflow an int.
    [GeneratedRegex(@"^extracting (?<done>[0-9]{1,9})/(?<total>[0-9]{1,9}) files\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FilesLine { get; }
}

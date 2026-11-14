using System.Globalization;
using System.Text.RegularExpressions;

namespace Aw.CodeIndex.Core.Facade;

/// <summary>
/// How far a code index build has read a repo's files, as its structure progress line says it ("structure 7,000 of 15,223 files"). The build writes the line and Sindri reads it
/// back for the setup list's map step (#3112), so both sides go through this one type and the text can't drift.
/// </summary>
/// <param name="Done">The files parsed so far.</param>
/// <param name="Total">The files the build parses.</param>
public sealed partial record IndexBuildFiles(int Done, int Total)
{
    /// <summary>The progress line: "structure &lt;done&gt; of &lt;total&gt; files", counts with thousands separators.</summary>
    public string Line => string.Create(CultureInfo.InvariantCulture, $"structure {Done:N0} of {Total:N0} files");

    /// <summary>The counts a progress line carries, or null when it is not a structure line (or done is above total).</summary>
    /// <param name="line">A job progress line.</param>
    /// <returns>The counts, or null.</returns>
    public static IndexBuildFiles? From(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var match = StructureLine.Match(line);
        if (!match.Success)
        {
            return null;
        }

        var done = int.Parse(match.Groups["done"].Value.Replace(",", string.Empty, StringComparison.Ordinal), CultureInfo.InvariantCulture);
        var total = int.Parse(match.Groups["total"].Value.Replace(",", string.Empty, StringComparison.Ordinal), CultureInfo.InvariantCulture);
        return done <= total ? new IndexBuildFiles(done, total) : null;
    }

    /// <summary>The counts of the newest structure line, or null when no line is one; later lines of other kinds ("graph", "embeddings") don't hide it.</summary>
    /// <param name="lines">A job's progress lines, oldest first.</param>
    /// <returns>The counts, or null.</returns>
    public static IndexBuildFiles? Newest(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        IndexBuildFiles? newest = null;
        foreach (var line in lines)
        {
            newest = From(line) ?? newest;
        }

        return newest;
    }

    // A count is 1 to 3 digits, then groups of 3 after a comma, 9 digits at most, so the parse can't overflow an int.
    [GeneratedRegex(@"^structure (?<done>[0-9]{1,3}(,[0-9]{3}){0,2}) of (?<total>[0-9]{1,3}(,[0-9]{3}){0,2}) files\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex StructureLine { get; }
}

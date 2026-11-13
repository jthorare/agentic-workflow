using System.Globalization;

namespace Aw.Context.Core.Extract;

/// <summary>
/// How far a map build has read one repo's files, as its progress line says it ("extracting 150/412 files"). The engine writes the line and Sindri reads it back for
/// the whole map's 'building' answer, so both sides go through this one type and the text can't drift. The count is per repo: a build of two repos starts again at 0 for the second.
/// </summary>
/// <param name="Done">The files read so far.</param>
/// <param name="Total">The files the build reads in this repo.</param>
public sealed record MapBuildFiles(int Done, int Total)
{
    /// <summary>The progress line: "extracting &lt;done&gt;/&lt;total&gt; files".</summary>
    public string Line => string.Create(CultureInfo.InvariantCulture, $"extracting {Done}/{Total} files");

    /// <summary>The counts a progress line carries, or null when it is not a files line (or its counts make no sense: negative, or done above total).</summary>
    /// <param name="line">A job progress line.</param>
    /// <returns>The counts, or null.</returns>
    public static MapBuildFiles? From(string line) => throw new NotSupportedException();
}

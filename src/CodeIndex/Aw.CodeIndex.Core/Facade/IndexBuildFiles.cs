namespace Aw.CodeIndex.Core.Facade;

/// <summary>
/// How far a code index build has read a repo's files, as its structure progress line says it ("structure 7,000 of 15,223 files"). The build writes the line and Sindri reads it
/// back for the setup list's map step (#3112), so both sides go through this one type and the text can't drift.
/// </summary>
/// <param name="Done">The files parsed so far.</param>
/// <param name="Total">The files the build parses.</param>
public sealed record IndexBuildFiles(int Done, int Total)
{
    /// <summary>The progress line: "structure &lt;done&gt; of &lt;total&gt; files", counts with thousands separators.</summary>
    public string Line => Total < 0 ? string.Empty : throw new NotSupportedException();

    /// <summary>The counts a progress line carries, or null when it is not a structure line (or done is above total).</summary>
    /// <param name="line">A job progress line.</param>
    /// <returns>The counts, or null.</returns>
    public static IndexBuildFiles? From(string line) => throw new NotSupportedException();

    /// <summary>The counts of the newest structure line, or null when no line is one; later lines of other kinds ("graph", "embeddings") don't hide it.</summary>
    /// <param name="lines">A job's progress lines, oldest first.</param>
    /// <returns>The counts, or null.</returns>
    public static IndexBuildFiles? Newest(IEnumerable<string> lines) => throw new NotSupportedException();
}

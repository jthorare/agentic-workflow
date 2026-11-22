namespace Aw.Transcripts.Infrastructure.Reading;

/// <summary>One provider's line rules. A reader is made per file, so it may carry state from line to line.</summary>
internal interface ILineReader
{
    /// <summary>Reads one non-blank line into <paramref name="file"/>; a line of the wrong shape counts as skipped.</summary>
    /// <param name="number">The 1-based line number.</param>
    /// <param name="line">The line.</param>
    /// <param name="file">What the pass found so far.</param>
    public void Read(int number, string line, ParsedFile file);
}

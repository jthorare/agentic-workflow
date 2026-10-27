using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Transcripts;
using Aw.Kernel;

namespace Aw.Judge.Core.Ports;

/// <summary>Reads the Claude transcripts (one .jsonl file per session under ~/.claude/projects). Every call returns an error, never throws, when a transcript can't be read.</summary>
public interface ITranscriptReader
{
    /// <summary>Finds and reads the transcript of a session.</summary>
    /// <param name="session">The session id (the file is named after it).</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The transcript, or AW-JUDGE-015 when no readable transcript of that session exists (also when the projects directory is missing).</returns>
    public ValueTask<Result<Transcript>> FindSession(SessionId session, CancellationToken cancellationToken);

    /// <summary>Reads the transcript at a path.</summary>
    /// <param name="path">The .jsonl file.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The transcript, or AW-JUDGE-015 when the file can't be read. A line that isn't JSON is skipped.</returns>
    public ValueTask<Result<Transcript>> Read(string path, CancellationToken cancellationToken);
}

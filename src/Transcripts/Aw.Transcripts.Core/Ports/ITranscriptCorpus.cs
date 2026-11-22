using Aw.Kernel;
using Aw.Transcripts.Core.Refs;
using Aw.Transcripts.Core.Sessions;

namespace Aw.Transcripts.Core.Ports;

/// <summary>
/// Every local agent transcript (Claude Code, Codex, Cursor) as one provider-neutral corpus. It reads files only; no
/// model is called. All text it returns has been through the secret scrubber.
/// </summary>
public interface ITranscriptCorpus
{
    /// <summary>The sessions written since a time, newest first.</summary>
    /// <param name="query">The time and filters.</param>
    /// <param name="cancellationToken">Cancels the scan.</param>
    /// <returns>The sessions; a missing root adds none.</returns>
    public ValueTask<Result<IReadOnlyList<TranscriptSession>>> ListSessions(SessionQuery query, CancellationToken cancellationToken);

    /// <summary>One session's turns.</summary>
    /// <param name="session">The session's ref.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The transcript, AW-TRANSCRIPTS-002 when no file has the ref, AW-TRANSCRIPTS-003 when it can't be read, AW-TRANSCRIPTS-004 when two files share it.</returns>
    public ValueTask<Result<SessionTranscript>> ReadSession(SessionRef session, CancellationToken cancellationToken);
}

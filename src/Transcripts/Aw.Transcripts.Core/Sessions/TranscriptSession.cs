using Aw.Transcripts.Core.Refs;

namespace Aw.Transcripts.Core.Sessions;

/// <summary>One transcript file as a session of the corpus.</summary>
/// <param name="Ref">The session's opaque ref.</param>
/// <param name="Provider">The agent that wrote it.</param>
/// <param name="StartedAt">The first timestamp in the file (Cursor: the first prompt's, else the write time).</param>
/// <param name="LastWrittenAt">The file's last write time.</param>
/// <param name="Repo">The main repo the session ran in: its cwd, with a worktree mapped to its main checkout while the worktree exists; the cwd itself otherwise; null when the transcript names no cwd (Cursor).</param>
/// <param name="IsSubagent">Whether a parent session spawned this one.</param>
/// <param name="Parent">The parent session's ref, when the parent's file is under the roots.</param>
/// <param name="HumanTurns">The prompts a person typed in this file, without the ones it copied from an earlier file.</param>
public sealed record TranscriptSession(
    SessionRef Ref,
    TranscriptProvider Provider,
    DateTimeOffset StartedAt,
    DateTimeOffset LastWrittenAt,
    string? Repo,
    bool IsSubagent,
    SessionRef? Parent,
    int HumanTurns);

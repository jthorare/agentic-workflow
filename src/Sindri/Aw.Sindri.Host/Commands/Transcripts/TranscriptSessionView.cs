namespace Aw.Sindri.Host.Commands.Transcripts;

/// <summary>One session in the CLI's JSON.</summary>
/// <param name="Ref">The opaque session ref, 'transcript:&lt;12 hex&gt;'.</param>
/// <param name="Provider">'claude-code', 'codex' or 'cursor'.</param>
/// <param name="StartedAt">The first timestamp in the file.</param>
/// <param name="LastWrittenAt">The file's write time.</param>
/// <param name="Repo">The main checkout the session ran in, or null.</param>
/// <param name="Subagent">Whether a parent session spawned it.</param>
/// <param name="Parent">The parent's ref, or null.</param>
/// <param name="HumanTurns">The prompts a person typed, without resume copies.</param>
internal sealed record TranscriptSessionView(string Ref, string Provider, DateTimeOffset StartedAt, DateTimeOffset LastWrittenAt, string? Repo, bool Subagent, string? Parent, int HumanTurns);

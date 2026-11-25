namespace Aw.Sindri.Testing.Workflow;

/// <summary>One session a <see cref="GraphTranscriptPortContract"/> test seeds.</summary>
/// <param name="Session">The session's ref, such as 'transcript:00000a1b2c3d'.</param>
/// <param name="Provider">'claude-code', 'codex' or 'cursor'.</param>
/// <param name="Repo">The main repo it ran in, or null.</param>
/// <param name="IsSubagent">Whether a parent session spawned it.</param>
/// <param name="Turns">Its turns, in file order.</param>
public sealed record TranscriptSeed(string Session, string Provider, string? Repo, bool IsSubagent, IReadOnlyList<TranscriptSeedTurn> Turns);

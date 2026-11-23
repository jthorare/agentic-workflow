namespace Aw.Sindri.Host.Commands.Transcripts;

/// <summary>'sindri transcripts list --json': the sessions written since a time, newest first.</summary>
/// <param name="Since">The time asked for.</param>
/// <param name="Fenced">Whether transcript text is fenced as untrusted (a session's call); a list carries none, so it is always false.</param>
/// <param name="Sessions">The sessions.</param>
internal sealed record TranscriptListView(DateTimeOffset Since, bool Fenced, IReadOnlyList<TranscriptSessionView> Sessions);

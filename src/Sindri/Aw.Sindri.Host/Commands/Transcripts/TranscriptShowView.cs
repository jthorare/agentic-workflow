namespace Aw.Sindri.Host.Commands.Transcripts;

/// <summary>'sindri transcripts show &lt;ref&gt; --json': one session's turns.</summary>
/// <param name="Session">The session.</param>
/// <param name="Fenced">Whether each turn's text is wrapped in an &lt;untrusted source="transcript"&gt; fence (a session's call).</param>
/// <param name="SkippedLines">Lines of the file that were not a JSON object of the provider's shape.</param>
/// <param name="Turns">The turns in file order.</param>
internal sealed record TranscriptShowView(TranscriptSessionView Session, bool Fenced, int SkippedLines, IReadOnlyList<TranscriptTurnView> Turns);

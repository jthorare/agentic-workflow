namespace Aw.Sindri.Host.Commands.Transcripts;

/// <summary>One turn in the CLI's JSON.</summary>
/// <param name="Ref">The turn ref, 'transcript:&lt;8 hex&gt;#&lt;line&gt;'.</param>
/// <param name="Role">'human' or 'assistant'.</param>
/// <param name="At">When the turn started.</param>
/// <param name="Text">The scrubbed text, fenced when <see cref="TranscriptShowView.Fenced"/>.</param>
/// <param name="Tools">An assistant turn's tool names; empty for a human turn.</param>
internal sealed record TranscriptTurnView(string Ref, string Role, DateTimeOffset At, string Text, IReadOnlyList<string> Tools);

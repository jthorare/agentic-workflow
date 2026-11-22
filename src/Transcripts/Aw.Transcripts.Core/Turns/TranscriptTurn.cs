using Aw.Transcripts.Core.Refs;

namespace Aw.Transcripts.Core.Turns;

/// <summary>One turn of a session. Its text has been through the secret scrubber.</summary>
/// <param name="Ref">The turn's ref: the session's ref and the line the turn starts on.</param>
/// <param name="Role">Human or assistant.</param>
/// <param name="At">When the turn started (for Cursor, the last timestamp the prompt carried, else the file's write time).</param>
/// <param name="Text">The scrubbed text: the prompt, or the assistant's text blocks joined by newlines (may be empty).</param>
/// <param name="Tools">An assistant turn's tool names, each once, in first-use order; no tool input or output. Empty for a human turn.</param>
public sealed record TranscriptTurn(TurnRef Ref, TurnRole Role, DateTimeOffset At, string Text, IReadOnlyList<string> Tools);

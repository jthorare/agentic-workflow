namespace Aw.Sindri.Testing.Workflow;

/// <summary>One turn of a <see cref="TranscriptSeed"/>.</summary>
/// <param name="Line">The 1-based line the turn starts on; the turn's ref is the session's ref, '#' and this line.</param>
/// <param name="Human">True for a prompt a person typed, false for an assistant turn.</param>
/// <param name="At">When the turn started.</param>
/// <param name="Text">The turn's text.</param>
public sealed record TranscriptSeedTurn(int Line, bool Human, DateTimeOffset At, string Text);

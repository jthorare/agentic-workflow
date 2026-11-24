namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>One human turn as 'transcript_turns' lists it: no text, so a list of hundreds stays small.</summary>
/// <param name="Ref">The turn's opaque ref, such as 'transcript:00000a1b2c3d#42'.</param>
/// <param name="Session">The session's opaque ref, such as 'transcript:00000a1b2c3d'.</param>
/// <param name="At">When the turn started.</param>
public sealed record HumanTurnRef(string Ref, string Session, DateTimeOffset At);

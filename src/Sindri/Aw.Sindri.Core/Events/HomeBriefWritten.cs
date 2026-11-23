namespace Aw.Sindri.Core.Events;

/// <summary>The brief job wrote 'since you last looked' (home spec §6.1).</summary>
/// <param name="At">When it was written.</param>
/// <param name="Mode">'auto' or 'facts'.</param>
/// <param name="Lines">The brief's lines, scrubbed.</param>
public sealed record HomeBriefWritten(DateTimeOffset At, string Mode, IReadOnlyList<string> Lines);

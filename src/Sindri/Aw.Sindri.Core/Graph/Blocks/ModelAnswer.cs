namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>What a model call gave back: its text and the exit code of the CLI that produced it.</summary>
/// <param name="Text">The answer text.</param>
/// <param name="ExitCode">The CLI's exit code; 0 for a port with no CLI.</param>
public sealed record ModelAnswer(string Text, int ExitCode = 0);

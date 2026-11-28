namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>What a model call gave back: its text and the exit code of the CLI that produced it.</summary>
/// <param name="Text">The answer text.</param>
/// <param name="ExitCode">The CLI's exit code; 0 for a port with no CLI.</param>
public sealed record ModelAnswer(string Text, int ExitCode = 0)
{
    /// <summary>Gets the tokens the call used, input and output together (#3805); 0 when the port can't tell.</summary>
    public long Tokens { get; init; }
}

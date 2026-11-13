namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>A launched session that waits on a permission prompt (#3151): the tool it asks to use, never the tool's input, and when the hook saw it.</summary>
/// <param name="Tool">The tool name ('Bash'); null when the state file's name did not match the hook's own pattern.</param>
/// <param name="At">When the aw:fleet hook wrote the state.</param>
internal sealed record NodeSessionAsk(string? Tool, DateTimeOffset At);

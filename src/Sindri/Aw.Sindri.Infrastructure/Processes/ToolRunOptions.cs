namespace Aw.Sindri.Infrastructure.Processes;

/// <summary>What a tool run may add to the plain call.</summary>
/// <param name="StandardErrorLine">Called once per standard error line while the tool runs (a pull's progress); null drops standard error. Standard output is still captured whole.</param>
/// <param name="StandardInput">Text written to the tool's standard input before it is closed (how a secret reaches a tool without being an argument); null writes nothing.</param>
/// <param name="Environment">Changes to the environment the tool inherits, by variable name: a value sets it, null removes it from the child; null leaves the environment as it is. The parent's own environment is never touched.</param>
/// <param name="WorkingDirectory">The directory the tool runs in; null: this process's own.</param>
internal sealed record ToolRunOptions(Action<string>? StandardErrorLine = null, IReadOnlyDictionary<string, string?>? Environment = null, string? StandardInput = null, string? WorkingDirectory = null);

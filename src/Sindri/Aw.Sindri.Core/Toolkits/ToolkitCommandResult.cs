namespace Aw.Sindri.Core.Toolkits;

/// <summary>How a command in a toolkit's folder ended.</summary>
/// <param name="ExitCode">The process exit code; 0 is success.</param>
/// <param name="Output">What it printed on standard output, bounded.</param>
/// <param name="Error">What it printed on standard error, bounded.</param>
public sealed record ToolkitCommandResult(int ExitCode, string Output, string Error = "");

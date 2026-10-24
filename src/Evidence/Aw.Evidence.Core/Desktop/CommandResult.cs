namespace Aw.Evidence.Core.Desktop;

/// <summary>How a command ended.</summary>
/// <param name="ExitCode">Its exit code.</param>
/// <param name="StandardOutput">What it wrote to standard output.</param>
/// <param name="StandardError">What it wrote to standard error.</param>
public sealed record CommandResult(int ExitCode, string StandardOutput, string StandardError);

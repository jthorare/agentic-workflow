namespace Aw.Forge.Infrastructure.Git;

/// <summary>What a finished process left behind.</summary>
/// <param name="ExitCode">The exit code, or -1 when it was killed on a timeout.</param>
/// <param name="Output">Standard output.</param>
/// <param name="Error">Standard error.</param>
internal sealed record ProcessResult(int ExitCode, string Output, string Error);

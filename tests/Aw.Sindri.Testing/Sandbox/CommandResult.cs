namespace Aw.Sindri.Testing.Sandbox;

/// <summary>How a command run on the host or inside a sandbox ended.</summary>
/// <param name="Exit">The exit code.</param>
/// <param name="Output">Standard output and standard error together.</param>
public sealed record CommandResult(int Exit, string Output);

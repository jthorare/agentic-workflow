namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>Runs the sandbox's helper programs (bwrap, systemd-run, git) to completion with an argument list, never a shell string.</summary>
internal interface ISandboxProcesses
{
    /// <summary>Runs the program and waits for it, killing it past <paramref name="timeout"/>.</summary>
    /// <param name="program">The program: an absolute path or a name looked up on PATH.</param>
    /// <param name="arguments">Its arguments, one per element.</param>
    /// <param name="timeout">How long it may run.</param>
    /// <returns>How it ended.</returns>
    public ProcessResult Run(string program, IReadOnlyList<string> arguments, TimeSpan timeout);
}

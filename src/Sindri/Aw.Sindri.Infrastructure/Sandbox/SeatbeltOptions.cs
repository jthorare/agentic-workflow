using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>The programs the Seatbelt adapter runs and how long it waits for them.</summary>
/// <param name="SandboxExec">The sandbox-exec binary: a name on PATH or an absolute path.</param>
/// <param name="Git">The git binary the host-side cleanup runs.</param>
/// <param name="CanaryTimeout">How long one canary may run.</param>
/// <param name="SelfCheckDeadline">How long all the canaries together may take.</param>
internal sealed record SeatbeltOptions(string SandboxExec, string Git, TimeSpan CanaryTimeout, TimeSpan SelfCheckDeadline)
{
    /// <summary>Reads the program from the sandbox's redirect variable (a path a test points elsewhere; it never switches the sandbox off).</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <returns>The options.</returns>
    internal static SeatbeltOptions From(Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var exec = environment(SandboxEnvironment.SeatbeltExec) is { Length: > 0 } value ? value : "/usr/bin/sandbox-exec";
        return new SeatbeltOptions(exec, "git", TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(4));
    }
}

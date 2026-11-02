using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>The programs the bubblewrap adapter runs and how long it waits for them.</summary>
/// <param name="Bwrap">The bubblewrap binary: a name on PATH or an absolute path.</param>
/// <param name="SystemdRun">The systemd-run binary.</param>
/// <param name="Socat">The socat binary the forwarders need.</param>
/// <param name="Git">The git binary the host-side cleanup runs.</param>
/// <param name="CanaryTimeout">How long one canary may run.</param>
/// <param name="SelfCheckDeadline">How long all the canaries together may take: the self-check sits in the 4 s host-start stage of the launch (spec §6).</param>
internal sealed record BubblewrapOptions(string Bwrap, string SystemdRun, string Socat, string Git, TimeSpan CanaryTimeout, TimeSpan SelfCheckDeadline)
{
    /// <summary>Reads the programs from the sandbox's redirect variables (paths a test points elsewhere; none of them switches the sandbox off).</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <returns>The options.</returns>
    internal static BubblewrapOptions From(Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        string Pick(string variable, string fallback) => environment(variable) is { Length: > 0 } value ? value : fallback;
        return new BubblewrapOptions(Pick(SandboxEnvironment.Bwrap, "bwrap"), Pick(SandboxEnvironment.SystemdRun, "systemd-run"), Pick(SandboxEnvironment.Socat, "socat"), "git", TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(4));
    }
}

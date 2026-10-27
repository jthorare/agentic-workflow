using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>The one place the integration tests make the adapter.</summary>
internal static class BubblewrapBox
{
    internal static BubblewrapOptions Options { get; } = BubblewrapOptions.From(Environment.GetEnvironmentVariable);

    /// <summary>Why bubblewrap cannot run on this box, or null when it can: the integration tests skip cleanly on a box without it.</summary>
    internal static string? Unavailable { get; } = Detect();

    internal static string FixtureParent { get; } = Path.Combine(AppContext.BaseDirectory, "sandbox-fixtures");

    internal static BubblewrapSandbox Create() => new(Options, new SandboxProcesses(), new HostPaths(), new LaunchSecretRegistry(), RealTime.Clock);

    private static string? Detect()
    {
        var probe = new SandboxProcesses().Run(Options.Bwrap, ["--ro-bind", "/", "/", "--unshare-user", "--unshare-net", "true"], TimeSpan.FromSeconds(10));
        return probe.Succeeded ? null : "bwrap cannot create a sandbox here: " + probe.Error.Trim();
    }
}

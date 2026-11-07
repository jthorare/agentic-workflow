using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>The one place the integration tests make the adapter.</summary>
internal static class BubblewrapBox
{
    internal static BubblewrapOptions Options { get; } = BubblewrapOptions.From(Environment.GetEnvironmentVariable);

    /// <summary>Why bubblewrap cannot run on this box, or null when it can: the integration tests skip cleanly on a box without it.</summary>
    internal static string? Unavailable { get; } = Detect();

    internal static string FixtureParent { get; } = Path.Combine(AppContext.BaseDirectory, "sandbox-fixtures");

    internal static BubblewrapSandbox Create() => new(Options, new SandboxProcesses(), new HostPaths(), new AnyLaunchSecret(), RealTime.Clock);

    private static string? Detect()
    {
        var probe = new SandboxProcesses().Run(Options.Bwrap, ["--ro-bind", "/", "/", "--unshare-user", "--unshare-net", "true"], TimeSpan.FromSeconds(10));
        return probe.Succeeded ? null : "bwrap cannot create a sandbox here: " + probe.Error.Trim();
    }

    /// <summary>Every launch has a launch secret, as the launcher gives each real one (#2163): a real plan leaves AW_SINDRI_LAUNCH_SECRET to the launcher, and bubblewrap refuses to wrap it without a value.</summary>
    private sealed class AnyLaunchSecret : ILaunchSecrets
    {
        public IReadOnlyDictionary<string, string> For(LaunchId launch) => new Dictionary<string, string>(StringComparer.Ordinal) { ["AW_SINDRI_LAUNCH_SECRET"] = "test-launch-secret" };
    }
}

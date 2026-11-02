using System.Globalization;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>Builds the systemd-run command that puts one session in its cgroup scope (sandbox spec §3.3; spike item 4). Pure.</summary>
internal static class SystemdScopeArgv
{
    /// <summary>The slice every Step's scope lives in (it nests as sindri.slice/sindri-steps.slice).</summary>
    internal const string Slice = "sindri-steps.slice";

    /// <summary>Gets the scope unit for a launch.</summary>
    /// <param name="launch">The launch.</param>
    /// <returns>The unit name.</returns>
    internal static string UnitFor(LaunchId launch) => $"sindri-{launch.Value}.scope";

    /// <summary>Builds the whole command: the systemd-run program, the scope's properties, '--' and the inner command.</summary>
    /// <param name="systemdRun">The systemd-run program (the caller resolves AW_SANDBOX_SYSTEMD_RUN).</param>
    /// <param name="launch">The launch.</param>
    /// <param name="limits">The limits.</param>
    /// <param name="inner">The command to run in the scope (the bubblewrap command).</param>
    /// <returns>The argv.</returns>
    internal static IReadOnlyList<string> Start(string systemdRun, LaunchId launch, ScopeLimits limits, IReadOnlyList<string> inner)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(inner);
        List<string> a = [systemdRun, "--user", "--scope", "--quiet", $"--unit={UnitFor(launch)}", $"--slice={Slice}"];
        AddProperty(a, "MemoryHigh", limits.MemoryHighBytes);
        AddProperty(a, "MemoryMax", limits.MemoryMaxBytes);
        a.AddRange(["-p", "MemorySwapMax=0", "-p", "OOMPolicy=kill"]);
        AddProperty(a, "CPUWeight", limits.CpuWeight);
        a.AddRange(["-p", $"CPUQuota={limits.CpuQuotaPercent.ToString(CultureInfo.InvariantCulture)}%"]);
        AddProperty(a, "TasksMax", limits.TasksMax);
        if (limits.Wall is { } wall)
        {
            // The loose ceiling: only a backstop for a watchdog that died. The watchdog's WallBudget is the clock.
            AddProperty(a, "RuntimeMaxSec", (long)(wall + limits.ParkMax).TotalSeconds);
        }

        a.Add("--");
        a.AddRange(inner);
        return a;
    }

    private static void AddProperty(List<string> a, string name, long value) => a.AddRange(["-p", $"{name}={value.ToString(CultureInfo.InvariantCulture)}"]);
}

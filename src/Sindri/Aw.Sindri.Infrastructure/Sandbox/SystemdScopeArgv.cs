using System.Globalization;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>The command lines that make and steer a session's cgroup scope under the parent slice (sandbox spec §3.3, SB7); pure.</summary>
internal static class SystemdScopeArgv
{
    /// <summary>The parent slice every Step session lives under.</summary>
    internal const string Slice = "sindri-steps.slice";

    /// <summary>Gets the scope unit of a launch.</summary>
    /// <param name="launch">The launch.</param>
    /// <returns>The unit name.</returns>
    internal static string UnitFor(LaunchId launch) => $"sindri-{launch.Value}.scope";

    /// <summary>Wraps <paramref name="argv"/> in 'systemd-run --user --scope' with the plan's limits as properties.</summary>
    /// <param name="systemdRun">The systemd-run binary.</param>
    /// <param name="limits">The plan's limits.</param>
    /// <param name="unit">The scope unit.</param>
    /// <param name="argv">What runs in the scope (the bubblewrap command).</param>
    /// <returns>The command line.</returns>
    internal static IReadOnlyList<string> Start(string systemdRun, PlanLimits limits, string unit, IReadOnlyList<string> argv)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(argv);
        List<string> command = [systemdRun, "--user", "--scope", "--quiet", $"--unit={unit}", $"--slice={Slice}"];
        foreach (var property in Properties(limits))
        {
            command.Add("-p");
            command.Add(property);
        }

        command.Add("--");
        command.AddRange(argv);
        return command;
    }

    // MemoryHigh throttles instead of killing (spike item 4); the kill comes at MemoryMax, and a scope with no swap and OOMPolicy=kill ends as a whole.
    private static IEnumerable<string> Properties(PlanLimits limits)
    {
        yield return $"MemoryHigh={Number(limits.MemoryHighBytes)}";
        yield return $"MemoryMax={Number(limits.MemoryMaxBytes)}";
        yield return "MemorySwapMax=0";
        yield return "OOMPolicy=kill";
        yield return "CPUWeight=50";
        yield return $"CPUQuota={Number(limits.CpuQuotaPercent)}%";
        yield return $"TasksMax={Number(limits.TasksMax)}";
        if (limits.RuntimeMax is { } ceiling)
        {
            yield return $"RuntimeMaxSec={Seconds(ceiling)}";
        }
    }

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Seconds(TimeSpan span) => Number((long)Math.Ceiling(span.TotalSeconds));

    /// <summary>The arguments to 'systemctl' that put the total memory cap on the parent slice.</summary>
    /// <param name="limits">The plan's limits.</param>
    /// <returns>The arguments.</returns>
    internal static IReadOnlyList<string> SliceCap(PlanLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        return ["--user", "set-property", "--runtime", Slice, $"MemoryMax={Number(limits.TotalMemoryBytes)}"];
    }

    /// <summary>The arguments to 'systemctl' that move a scope's loose ceiling.</summary>
    /// <param name="unit">The scope unit.</param>
    /// <param name="runtimeMax">The new 'RuntimeMaxSec', counted from the scope's start.</param>
    /// <returns>The arguments.</returns>
    internal static IReadOnlyList<string> SetRuntimeMax(string unit, TimeSpan runtimeMax) =>
        ["--user", "set-property", "--runtime", unit, $"RuntimeMaxSec={Seconds(runtimeMax)}"];

    /// <summary>The arguments to 'systemctl' that signal every process of a scope.</summary>
    /// <param name="unit">The scope unit.</param>
    /// <param name="force">True for SIGKILL, false for SIGTERM.</param>
    /// <returns>The arguments.</returns>
    internal static IReadOnlyList<string> Kill(string unit, bool force) =>
        ["--user", "kill", force ? "--signal=SIGKILL" : "--signal=SIGTERM", "--kill-whom=all", unit];

    /// <summary>The arguments to 'systemctl' that read a scope's state, result and control group.</summary>
    /// <param name="unit">The scope unit.</param>
    /// <returns>The arguments.</returns>
    internal static IReadOnlyList<string> Show(string unit) =>
        ["--user", "show", unit, "-p", "ActiveState", "-p", "Result", "-p", "ControlGroup"];
}

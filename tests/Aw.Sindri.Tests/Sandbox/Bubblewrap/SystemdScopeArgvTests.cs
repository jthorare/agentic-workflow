using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>The cgroup scope's command lines (sandbox spec §3.3, SB7; spike item 4): the plan's limits as properties under the parent slice, the loose ceiling, the signals.</summary>
[Lane(TestLane.Unit)]
public sealed class SystemdScopeArgvTests
{
    private const long Gib = 1024L * 1024 * 1024;
    private static readonly PlanLimits _limits = new(
        MemoryMaxBytes: 4 * Gib,
        MemoryHighBytes: 4 * Gib * 8 / 10,
        CpuQuotaPercent: 400,
        TasksMax: 2048,
        Sessions: 3,
        TotalMemoryBytes: 10 * Gib,
        HomeTmpfsBytes: 256L * 1024 * 1024,
        WallGrace: TimeSpan.FromMinutes(10),
        ParkMax: TimeSpan.FromHours(24),
        Wall: TimeSpan.FromSeconds(15000),
        RuntimeMax: TimeSpan.FromSeconds(101400));

    [Fact]
    public void The_unit_is_named_for_the_launch() =>
        Expect.Equal($"sindri-{A.FirstLaunchId.Value}.scope", SystemdScopeArgv.UnitFor(A.FirstLaunchId));

    [Fact]
    public void Scope_properties_come_from_the_plan_limits() =>
        Expect.Sequence(
            [
                "systemd-run", "--user", "--scope", "--quiet", "--unit=sindri-unit.scope", "--slice=sindri-steps.slice",
                "-p", "MemoryHigh=3435973836", "-p", "MemoryMax=4294967296", "-p", "MemorySwapMax=0", "-p", "OOMPolicy=kill",
                "-p", "CPUWeight=50", "-p", "CPUQuota=400%", "-p", "TasksMax=2048", "-p", "RuntimeMaxSec=101400",
                "--", "bwrap", "--version",
            ],
            SystemdScopeArgv.Start("systemd-run", _limits, "sindri-unit.scope", ["bwrap", "--version"]));

    [Fact]
    public void An_attended_session_has_no_RuntimeMaxSec() =>
        Assert.DoesNotContain(SystemdScopeArgv.Start("systemd-run", _limits with { Wall = null, RuntimeMax = null }, "sindri-unit.scope", ["true"]), a => a.StartsWith("RuntimeMaxSec=", StringComparison.Ordinal));

    [Fact]
    public void The_parent_slice_is_capped_at_the_total_memory() =>
        Expect.Sequence(["--user", "set-property", "--runtime", "sindri-steps.slice", "MemoryMax=10737418240"], SystemdScopeArgv.SliceCap(_limits));

    [Fact]
    public void Moving_the_ceiling_sets_RuntimeMaxSec_in_whole_seconds_rounded_up() =>
        Expect.Sequence(["--user", "set-property", "--runtime", "sindri-unit.scope", "RuntimeMaxSec=10801"], SystemdScopeArgv.SetRuntimeMax("sindri-unit.scope", TimeSpan.FromHours(3) + TimeSpan.FromMilliseconds(500)));

    [Fact]
    public void The_stop_is_SIGTERM_and_the_force_is_SIGKILL_to_every_process_of_the_scope()
    {
        Expect.Sequence(["--user", "kill", "--signal=SIGTERM", "--kill-whom=all", "sindri-unit.scope"], SystemdScopeArgv.Kill("sindri-unit.scope", force: false));
        Expect.Sequence(["--user", "kill", "--signal=SIGKILL", "--kill-whom=all", "sindri-unit.scope"], SystemdScopeArgv.Kill("sindri-unit.scope", force: true));
    }

    [Fact]
    public void The_state_is_read_with_show() =>
        Expect.Sequence(["--user", "show", "sindri-unit.scope", "-p", "ActiveState", "-p", "Result", "-p", "ControlGroup"], SystemdScopeArgv.Show("sindri-unit.scope"));
}

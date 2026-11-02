using Aw.Kernel.Testing;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Scope;

/// <summary>The systemd-run, slice-cap and ceiling commands (sandbox spec §3.3; spike item 4).</summary>
[Lane(TestLane.Unit)]
public sealed class SystemdScopeArgvTests
{
    private const long Gib = 1024L * 1024 * 1024;
    private static readonly LaunchId _launch = LaunchId.From("LCH-" + new string('0', 31) + "1");
    private static readonly string _unit = "sindri-LCH-" + new string('0', 31) + "1.scope";

    private static ScopeLimits Limits(bool attended) =>
        ScopeLimits.From(new SandboxLimits(Memory: "8GiB", Total: "20GiB"), new BoxFacts(32 * Gib, 4), TimeSpan.FromMinutes(10), TimeSpan.FromHours(24), attended);

    [Fact]
    public void Scope_argv_carries_every_limit_and_the_loose_ceiling()
    {
        var argv = SystemdScopeArgv.Start("systemd-run", _launch, Limits(attended: false), ["bwrap", "--", "claude"]);

        Assert.Equal(
            [
                "systemd-run", "--user", "--scope", "--quiet", $"--unit={_unit}", "--slice=sindri-steps.slice",
                "-p", "MemoryHigh=6871947673", "-p", "MemoryMax=8589934592", "-p", "MemorySwapMax=0", "-p", "OOMPolicy=kill",
                "-p", "CPUWeight=50", "-p", "CPUQuota=200%", "-p", "TasksMax=2048",
                "-p", "RuntimeMaxSec=87600",   // (10 min + 10 min grace + 24 h) in seconds
                "--", "bwrap", "--", "claude",
            ],
            argv);
    }

    [Fact]
    public void An_attended_scope_has_no_RuntimeMaxSec() =>
        Assert.DoesNotContain(SystemdScopeArgv.Start("systemd-run", _launch, Limits(attended: true), ["claude"]), a => a.StartsWith("RuntimeMaxSec", StringComparison.Ordinal));

    [Fact]
    public void The_unit_is_named_for_the_launch() => Assert.Equal(_unit, SystemdScopeArgv.UnitFor(_launch));

    [Fact]
    public void The_slice_cap_sets_the_total_on_the_steps_slice() =>
        Assert.Equal(["systemctl", "--user", "set-property", "--runtime", "sindri-steps.slice", "MemoryMax=21474836480"], SliceCap.Argv("systemctl", Limits(attended: false)));

    [Fact]
    public void The_ceiling_command_sets_RuntimeMaxSec_on_the_unit() =>
        Assert.Equal(["systemctl", "--user", "set-property", "--runtime", _unit, "RuntimeMaxSec=3600"], RuntimeCeiling.Argv("systemctl", _unit, TimeSpan.FromHours(1)));
}

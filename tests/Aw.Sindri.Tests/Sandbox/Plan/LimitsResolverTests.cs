using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>The limits of a plan (sandbox spec §3.3, §3.9, §9).</summary>
public sealed class LimitsResolverTests
{
    private const long Gib = 1024L * 1024 * 1024;

    private static LimitsInput Step(SandboxLimits? limits = null, TimeSpan? timeout = null, long ram = 32 * Gib, int cores = 8, TimeSpan? longMax = null) =>
        new(Limits: limits, AttendedSettings: null, Attended: false, StepTimeout: timeout, LongMax: longMax, RamBytes: ram, Cores: cores);

    private static LimitsInput Attended(SandboxAttended? settings = null) =>
        new(Limits: null, AttendedSettings: settings, Attended: true, StepTimeout: null, LongMax: null, RamBytes: 32 * Gib, Cores: 8);

    [Fact, Trait("Lane", "unit")]
    public void Cpu_quota_is_cores_times_share()
    {
        Assert.Equal(400, LimitsResolver.Resolve(Step()).CpuQuotaPercent);
        Assert.Equal(200, LimitsResolver.Resolve(Step(new SandboxLimits(Cpu: "25%"))).CpuQuotaPercent);
    }

    [Fact, Trait("Lane", "unit")]
    public void Memory_auto_is_a_fifth_of_ram_at_most_12GiB()
    {
        var small = LimitsResolver.Resolve(Step(ram: 32 * Gib));
        var big = LimitsResolver.Resolve(Step(ram: 128 * Gib));

        Assert.Equal(32 * Gib / 5, small.MemoryMaxBytes);
        Assert.Equal(small.MemoryMaxBytes * 8 / 10, small.MemoryHighBytes);
        Assert.Equal(12 * Gib, big.MemoryMaxBytes);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_explicit_memory_and_total_win()
    {
        var limits = LimitsResolver.Resolve(Step(new SandboxLimits(Memory: "4GiB", Total: "10GiB")));

        Assert.Equal(4 * Gib, limits.MemoryMaxBytes);
        Assert.Equal(10 * Gib, limits.TotalMemoryBytes);
    }

    [Fact, Trait("Lane", "unit")]
    public void Defaults_are_sixty_percent_total_2048_tasks_3_sessions_and_a_256MiB_home()
    {
        var limits = LimitsResolver.Resolve(Step());

        Assert.Equal(32 * Gib * 60 / 100, limits.TotalMemoryBytes);
        Assert.Equal(2048, limits.TasksMax);
        Assert.Equal(3, limits.Sessions);
        Assert.Equal(256L * 1024 * 1024, limits.HomeTmpfsBytes);
        Assert.Equal(TimeSpan.FromMinutes(10), limits.WallGrace);
        Assert.Equal(TimeSpan.FromHours(24), limits.ParkMax);
    }

    [Fact, Trait("Lane", "unit")]
    public void Wall_is_timeout_plus_grace_and_the_ceiling_adds_park_max()
    {
        var limits = LimitsResolver.Resolve(Step(timeout: TimeSpan.FromMinutes(60)));

        Assert.Equal(TimeSpan.FromMinutes(70), limits.Wall);
        Assert.Equal(TimeSpan.FromMinutes(70) + TimeSpan.FromHours(24), limits.RuntimeMax);
    }

    [Fact, Trait("Lane", "unit")]
    public void Wall_defaults_to_four_hours_without_a_timeout() =>
        Assert.Equal(TimeSpan.FromMinutes(250), LimitsResolver.Resolve(Step()).Wall);

    [Fact, Trait("Lane", "unit")]
    public void Wall_is_at_most_long_max_plus_grace() =>
        Assert.Equal(TimeSpan.FromMinutes(370), LimitsResolver.Resolve(Step(timeout: TimeSpan.FromHours(10), longMax: TimeSpan.FromHours(6))).Wall);

    [Fact, Trait("Lane", "unit")]
    public void Attended_session_has_no_wall_watchdog_and_no_runtime_max()
    {
        var limits = LimitsResolver.Resolve(Attended());

        Assert.Null(limits.Wall);
        Assert.Null(limits.RuntimeMax);
        Assert.Equal(400, limits.CpuQuotaPercent);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_attended_wall_setting_is_a_ceiling()
    {
        var limits = LimitsResolver.Resolve(Attended(new SandboxAttended(Wall: "8h")));

        Assert.Equal(TimeSpan.FromHours(8), limits.Wall);
        Assert.Equal(TimeSpan.FromHours(8), limits.RuntimeMax);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_attended_wall_of_off_means_none()
    {
        var limits = LimitsResolver.Resolve(Attended(new SandboxAttended(Wall: "off")));

        Assert.Null(limits.Wall);
    }
}

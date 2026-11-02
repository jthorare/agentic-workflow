using Aw.Kernel.Testing;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Scope;

/// <summary>The numbers a session's scope is started with (sandbox spec §3.3).</summary>
[Lane(TestLane.Unit)]
public sealed class ScopeLimitsTests
{
    private const long Gib = 1024L * 1024 * 1024;
    private static readonly TimeSpan _longMax = TimeSpan.FromHours(24);

    private static ScopeLimits Limits(SandboxLimits? profile = null, long ramGib = 32, int cores = 8, TimeSpan? timeout = null, bool attended = false) =>
        ScopeLimits.From(profile, new BoxFacts(ramGib * Gib, cores), timeout, _longMax, attended);

    [Fact]
    public void Auto_memory_is_a_fifth_of_ram_capped_at_12_GiB()
    {
        Assert.Equal(32 * Gib / 5, Limits(ramGib: 32).MemoryMaxBytes);
        Assert.Equal(12 * Gib, Limits(ramGib: 128).MemoryMaxBytes);
    }

    [Fact]
    public void MemoryHigh_is_80_percent_of_max() => Assert.Equal(Limits(profile: new SandboxLimits(Memory: "10GiB")).MemoryMaxBytes / 10 * 8, Limits(profile: new SandboxLimits(Memory: "10GiB")).MemoryHighBytes);

    [Fact]
    public void CpuQuota_is_half_of_every_core() => Assert.Equal(400, Limits(cores: 8).CpuQuotaPercent);

    [Fact]
    public void Profile_values_win_over_defaults()
    {
        var l = Limits(new SandboxLimits(Memory: "6GiB", Cpu: "25%", Tasks: 100, Total: "40GiB", WallGrace: "5m", ParkMax: "2h"), timeout: TimeSpan.FromMinutes(20));

        Assert.Equal(6 * Gib, l.MemoryMaxBytes);
        Assert.Equal(200, l.CpuQuotaPercent);
        Assert.Equal(100, l.TasksMax);
        Assert.Equal(40 * Gib, l.SliceTotalBytes);
        Assert.Equal(TimeSpan.FromMinutes(25), l.Wall);
        Assert.Equal(TimeSpan.FromMinutes(5), l.WallGrace);
        Assert.Equal(TimeSpan.FromHours(2), l.ParkMax);
    }

    [Fact]
    public void Defaults_fill_what_the_profile_leaves_out()
    {
        var l = Limits(profile: null);

        Assert.Equal(32 * Gib / 100 * 60, l.SliceTotalBytes);
        Assert.Equal(2048, l.TasksMax);
        Assert.Equal(50, l.CpuWeight);
        Assert.Equal(TimeSpan.FromHours(24), l.ParkMax);
    }

    [Fact]
    public void Wall_is_timeout_plus_grace_and_4h_without_one_capped_at_longMax()
    {
        Assert.Equal(TimeSpan.FromMinutes(40), Limits(timeout: TimeSpan.FromMinutes(30)).Wall);
        Assert.Equal(TimeSpan.FromHours(4) + TimeSpan.FromMinutes(10), Limits().Wall);
        Assert.Equal(_longMax + TimeSpan.FromMinutes(10), Limits(timeout: TimeSpan.FromDays(3)).Wall);
    }

    [Fact]
    public void An_attended_session_has_no_wall() => Assert.Null(Limits(attended: true, timeout: TimeSpan.FromMinutes(5)).Wall);
}

using Aw.Meta.Core;
using Aw.Meta.Infrastructure.Files;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Meta.Tests.Adapters;

/// <summary>ProcProcessLiveness over a fake /proc, so it runs on any OS.</summary>
public sealed class ProcProcessLivenessTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    private static string Stat(long startTicks) =>
        "321 (agent) S 1 321 321 0 -1 4194560 100 0 0 0 1 2 0 0 20 0 3 0 " + startTicks.ToString(System.Globalization.CultureInfo.InvariantCulture) + " 1000000 2500 18446744073709551615";

    private static ProcProcessLiveness Make(Dictionary<string, string> files) =>
        new(new FakeTimeProvider(_now), (path, _) => files.TryGetValue(path, out var text) ? Task.FromResult(text) : throw new FileNotFoundException(path));

    [Fact, Trait("Lane", "unit")]
    public async Task A_process_that_started_before_the_file_was_written_is_alive()
    {
        // up 1000 s, started at tick 10000 (100 s after boot): 900 s old
        var liveness = Make(new(StringComparer.Ordinal) { ["/proc/321/stat"] = Stat(10_000), ["/proc/uptime"] = "1000.00 3000.00\n" });

        var alive = await liveness.IsRunning(321, _now, TestContext.Current.CancellationToken);

        Assert.True(alive.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_process_with_no_proc_entry_is_not_alive()
    {
        var liveness = Make(new(StringComparer.Ordinal) { ["/proc/uptime"] = "1000.00 3000.00\n" });

        var alive = await liveness.IsRunning(321, _now, TestContext.Current.CancellationToken);

        Assert.True(alive.IsSuccess);
        Assert.False(alive.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_uptime_smaller_than_the_start_time_is_inconsistent_clocks_AW_META_010_not_a_reused_pid()
    {
        // started at tick 100000 (1000 s after boot) but the host says it has been up 5 s
        var liveness = Make(new(StringComparer.Ordinal) { ["/proc/321/stat"] = Stat(100_000), ["/proc/uptime"] = "5.00 10.00\n" });

        var alive = await liveness.IsRunning(321, _now - TimeSpan.FromHours(1), TestContext.Current.CancellationToken);

        Assert.True(alive.IsError);
        Assert.Equal(MetaErrors.HostUnreadable.Code, alive.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unreadable_uptime_is_AW_META_010_not_a_process_that_is_not_running()
    {
        var liveness = Make(new(StringComparer.Ordinal) { ["/proc/321/stat"] = Stat(10_000) });

        var alive = await liveness.IsRunning(321, _now, TestContext.Current.CancellationToken);

        Assert.True(alive.IsError);
        Assert.Equal(MetaErrors.HostUnreadable.Code, alive.Error.Code);
    }
}

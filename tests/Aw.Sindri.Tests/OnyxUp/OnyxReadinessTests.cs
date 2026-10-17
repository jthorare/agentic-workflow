using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxUp;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

public sealed class OnyxReadinessTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static OnyxUpContext Context(OnyxUpRig rig) => new(rig.Pins.Pins, rig.Paths, 3000, "linux/arm64");

    private static Task<Kernel.Result<string>> Wait(OnyxUpRig rig)
        => new OnyxReadiness(rig.Health, rig.Runtime, rig.Clock).Wait(Context(rig), rig.Progress, Token).AsTask();

    // Lets the wait's pending delay fire, two seconds at a time, until it finishes.
    private static async Task Drive(Task task, OnyxUpRig rig)
    {
        for (var i = 0; i < 2000 && !task.IsCompleted; i++)
        {
            await Task.Delay(2, Token);
            rig.Clock.Advance(OnyxReadiness.Interval);
        }
    }

    private static async Task WaitForProbes(OnyxUpRig rig, int count)
    {
        for (var i = 0; i < 1000 && rig.Health.Probed.Count < count; i++)
        {
            await Task.Delay(2, Token);
        }

        Assert.True(rig.Health.Probed.Count >= count, "the wait never probed");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Waits_until_health_then_checks_the_version()
    {
        var rig = new OnyxUpRig();
        rig.Health.Enqueue(new OnyxProbe(Healthy: false, BackendVersion: null), new OnyxProbe(Healthy: true, BackendVersion: null), new OnyxProbe(Healthy: true, BackendVersion: "v4.9.0"));
        var wait = Wait(rig);

        await Drive(wait, rig);

        Assert.Equal("v4.9.0", (await wait).Value);
        Assert.Equal(3, rig.Health.Probed.Count);
        Assert.Equal(3, rig.Progress.Ticks);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_version_without_the_leading_v_is_the_pinned_one()
    {
        var rig = new OnyxUpRig();
        rig.Health.Default = new OnyxProbe(Healthy: true, BackendVersion: "4.9.0");

        Assert.Equal("4.9.0", (await Wait(rig)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_wrong_version_is_005_naming_both()
    {
        var rig = new OnyxUpRig();
        rig.Health.Default = new OnyxProbe(Healthy: true, BackendVersion: "v4.8.0");

        var error = (await Wait(rig)).Error;

        Assert.Equal("SND-ONYX-005", error.Code);
        Assert.Contains("v4.8.0", error.Message, StringComparison.Ordinal);
        Assert.Contains("v4.9.0", error.Message, StringComparison.Ordinal);
        _ = Assert.Single(rig.Health.Probed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_timeout_after_15_minutes_is_005_with_the_ps_summary()
    {
        var rig = new OnyxUpRig();
        rig.Health.Default = new OnyxProbe(Healthy: false, BackendVersion: null);
        rig.Runtime.Containers.Add(new ComposeContainer("api_server", "running", "unhealthy", "img", []));
        rig.Runtime.Containers.Add(new ComposeContainer("cache", "running", Health: null, "img", []));
        var wait = Wait(rig);
        await WaitForProbes(rig, 1);

        rig.Clock.Advance(TimeSpan.FromMinutes(15));
        var error = (await wait).Error;

        Assert.Equal("SND-ONYX-005", error.Code);
        Assert.Contains("api_server: running (unhealthy); cache: running", error.Message, StringComparison.Ordinal);
        Assert.Contains("15 minutes", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Polls_every_2_seconds()
    {
        var rig = new OnyxUpRig();
        rig.Health.Default = new OnyxProbe(Healthy: false, BackendVersion: null);
        var wait = Wait(rig);
        await WaitForProbes(rig, 1);

        for (var expected = 2; expected <= 6; expected++)
        {
            rig.Clock.Advance(OnyxReadiness.Interval);
            await WaitForProbes(rig, expected);
        }

        Assert.Equal(6, rig.Health.Probed.Count);
        rig.Clock.Advance(OnyxReadiness.Deadline);
        _ = await wait;
    }
}

using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxUp;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

public sealed class OnyxUpStartRetryTests
{
    private const string Note = "Onyx's api_server is still starting (it restarts once while Postgres comes up); waiting";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Kernel.Error Unhealthy => SindriErrors.OnyxComposeFailed.With("`docker compose up` failed (exit 1): dependency failed to start: container sindri-onyx-api_server-1 is unhealthy");

    // Every delay completes at once by moving the fake clock, so a retry loop runs to its end with no real waiting.
    private static async Task<Kernel.Result<OnyxUpResponse>> Drive(Task<Kernel.Result<OnyxUpResponse>> run, OnyxUpRig rig)
    {
        _ = rig;
        return await run;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unhealthy_dependency_is_waited_out_with_one_note()
    {
        var rig = new OnyxUpRig { Clock = { AdvanceOnDelay = true } };
        var failed = false;
        rig.Runtime.OnCall = op =>
        {
            if (op == "Up" && !failed)
            {
                failed = true;
                rig.Runtime.FailNext("Up", Unhealthy);
            }
        };

        var result = await Drive(rig.Up(Token), rig);

        Assert.False(result.IsError, result.IsError ? result.Error.Message : "");
        Assert.Equal(2, rig.Runtime.Calls.Count(static c => c == "Up"));
        Assert.Equal(1, rig.Progress.Lines.Count(static l => l == Note));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Another_compose_failure_is_011_after_one_call()
    {
        var rig = new OnyxUpRig { Clock = { AdvanceOnDelay = true } };
        rig.Runtime.OnCall = op => { if (op == "Up") { rig.Runtime.FailNext("Up", SindriErrors.OnyxComposeFailed.With("`docker compose up` failed (exit 1): port is already allocated")); } };

        var error = (await Drive(rig.Up(Token), rig)).Error;

        Assert.Equal("SND-ONYX-011", error.Code);
        Assert.Equal(1, rig.Runtime.Calls.Count(static c => c == "Up"));
        Assert.DoesNotContain(Note, rig.Progress.Lines, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_dependency_that_never_comes_up_is_005_at_the_deadline()
    {
        var rig = new OnyxUpRig { Clock = { AdvanceOnDelay = true } };
        rig.Runtime.OnCall = op => { if (op == "Up") { rig.Runtime.FailNext("Up", Unhealthy); } };

        var error = (await Drive(rig.Up(Token), rig)).Error;

        Assert.Equal("SND-ONYX-005", error.Code);
        Assert.Equal(1, rig.Progress.Lines.Count(static l => l == Note));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_compose_up_that_timed_out_inside_the_window_is_retried_like_a_starting_dependency()
    {
        var rig = new OnyxUpRig { Clock = { AdvanceOnDelay = true } };
        var failed = false;
        rig.Runtime.OnCall = op =>
        {
            if (op == "Up" && !failed)
            {
                failed = true;
                rig.Runtime.FailNext("Up", ComposeFailures.TimedOut("compose up", TimeSpan.FromMinutes(5)));
            }
        };

        var result = await Drive(rig.Up(Token), rig);

        Assert.False(result.IsError, result.IsError ? result.Error.Message : "");
        Assert.Equal(2, rig.Runtime.Calls.Count(static c => c == "Up"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Each_up_call_is_limited_to_the_time_left_in_the_window()
    {
        var rig = new OnyxUpRig { Clock = { AdvanceOnDelay = true } };
        rig.Runtime.OnCall = op => { if (op == "Up") { rig.Runtime.FailNext("Up", Unhealthy); } };

        var error = (await Drive(rig.Up(Token), rig)).Error;

        Assert.Equal("SND-ONYX-005", error.Code);
        var limits = rig.Runtime.UpLimits;
        Assert.True(limits.Count > 2);
        Assert.Equal(OnyxReadiness.Deadline, limits[0]);
        for (var i = 0; i < limits.Count; i++)
        {
            var left = OnyxReadiness.Deadline - (i * OnyxStartStep.RetryInterval);
            Assert.True(limits[i] > TimeSpan.Zero);
            Assert.True(limits[i] <= left);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_up_that_used_the_whole_window_leaves_readiness_one_probe_not_a_negative_wait()
    {
        var rig = new OnyxUpRig();
        rig.Runtime.OnCall = op => { if (op == "Up") { rig.Clock.Advance(TimeSpan.FromMinutes(16)); } };
        rig.Health.Default = new OnyxProbe(Healthy: false, BackendVersion: null);

        var error = (await rig.Up(Token)).Error;

        Assert.Equal("SND-ONYX-005", error.Code);
        _ = Assert.Single(rig.Health.Probed);
    }
}

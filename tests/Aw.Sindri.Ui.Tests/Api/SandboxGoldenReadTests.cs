using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Sessions;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>The sandbox members added to existing records (wire contract items 1, 2, 3, 6, 7), read from the spec-shaped goldens.</summary>
[Lane(TestLane.Unit)]
public sealed class SandboxGoldenReadTests
{
    private static T Read<T>(string slug, string scenario)
        where T : class =>
        AwJson.Deserialize<T>(Encoding.UTF8.GetBytes(GoldenFiles.SpecShaped(slug, scenario)), SindriApiClient.Json).Value;

    [Fact]
    public void A_session_row_carries_a_Step_an_attended_and_an_unavailable_sandbox()
    {
        var rows = Read<SessionList>("sessions", "sandbox-rows").Sessions;

        var step = rows[0].Sandbox!;
        Assert.Equal(("on", false, "/home/thor/repos/aw-wt/api-sb6", 2254857830L, 12884901888L), (step.State, step.Attended, step.Folder, step.Memory!.UsedBytes, step.Memory.MaxBytes));
        Assert.Equal((true, null), (rows[1].Sandbox!.Attended, rows[1].Sandbox!.Memory!.UsedBytes));
        Assert.Equal(("unavailable", null, null), (rows[2].Sandbox!.State, rows[2].Sandbox!.Folder, rows[2].Sandbox!.Memory));
    }

    [Fact]
    public void A_started_launch_carries_its_sandbox_summary_and_a_limit_ending_its_kind()
    {
        var started = Read<LaunchView>("launches-id", "sandbox-started");
        var ended = Read<LaunchView>("launches-id", "sandbox-ended-limit");

        Assert.Equal(("bubblewrap", "enforce", "12GiB", "none"), (started.Sandbox!.Mechanism, started.Sandbox.NetworkMode, started.Sandbox.Limits.MemoryMax, started.Sandbox.Credentials.Github));
        Assert.Equal(("limit", "memory", 12884901888L), (ended.Ended!.Reason, ended.Ended.Kind, ended.Ended.Limit));
        Assert.Equal(("ITM-0000000000000000000000000000000f/step/1", "build", 2), (started.Step!.Instance, started.Step.State, started.Step.Attempt));
    }

    [Fact]
    public void A_failed_launch_keeps_the_served_reason()
    {
        var failed = Read<LaunchView>("launches-id", "sandbox-failed");

        Assert.Equal(("SND-LAUNCH-014", "no-bwrap"), (failed.Code, failed.Reason));
    }
}

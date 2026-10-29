using System.Text;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Sessions;
using Aw.Sindri.Ui.Api.Workflow;
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

    [Fact]
    public void A_park_row_carries_the_blocker_and_its_gray_state()
    {
        var red = Assert.Single(Read<NeedsYouList>("needs-you", "sandbox-park-no-bwrap").Rows).Sandbox!;
        var gray = Assert.Single(Read<NeedsYouList>("needs-you", "sandbox-park-unsupported").Rows).Sandbox!;

        Assert.Equal(("no-bwrap", null, "sudo apt install bubblewrap"), (red.Reason, red.State, red.Command));
        Assert.Equal(("unsupported-platform", "gray", null), (gray.Reason, gray.State, gray.Command));
    }

    [Fact]
    public void An_instance_status_waiting_for_a_slot_says_how_many_run()
    {
        var status = Read<StatusFields>("workflow-status", "sandbox-waiting-for-slot");

        Assert.Equal((3, 3), (status.Run!.WaitingForSlot!.Running, status.Run.WaitingForSlot.Cap));
    }

    [Fact]
    public void A_limit_ending_event_carries_the_kind_the_limit_and_the_kept_branch_when_there_is_one()
    {
        var memory = Read<InstanceEventsView>("workflow-instance-events", "sandbox-limit-memory-branch").Events[^1].Transition!;
        var wall = Read<InstanceEventsView>("workflow-instance-events", "sandbox-limit-wall-no-branch").Events[^1].Transition!;

        Assert.Equal(("limit", "memory", "12GiB", "1260", "sindri/itm-0f-review-1"), (memory.Reason, memory.Limit!.Kind, memory.Limit.Limit, memory.Limit.Elapsed, memory.Limit.Branch));
        Assert.Equal(("wall", "40m", null), (wall.Limit!.Kind, wall.Limit.Limit, wall.Limit.Branch));
    }
}

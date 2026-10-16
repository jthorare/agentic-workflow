using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>What 'GetInstance' renders into the view (engine spec §3.8, §6.4): detail, 'next', answer lines, commands, budget.</summary>
[Lane(TestLane.Unit)]
public sealed class InstanceViewsTests
{
    [Fact]
    public void A_desk_task_fills_the_recipe_next_and_names_the_complete_line_with_its_required_output()
    {
        var view = StatusCases.View("desk-task");

        Assert.Equal(("produce", "desk task"), (view.State, view.Detail));
        Assert.Equal(
            "Produce the Step output for T-3 (round 1/3), then run:\nsindri workflow complete T-3/step/1 --run 1.1 --outcome done --output fact:output=<file>",
            view.Next);
        Assert.Equal("complete, takeover, cancel", StatusLayout.Commands(view));
        Assert.Equal((0, 200_000, 0, 400_000), (view.Budget!.State, view.Budget.StateCap, view.Budget.Instance, view.Budget.InstanceCap));
        Assert.Null(view.Answer);
    }

    [Theory]
    [InlineData("parked-rounds", "rounds", "sindri workflow answer T-3/step/1 --grant 1     (at a terminal)")]
    [InlineData("parked-no-progress-child", "no-progress (child)", "sindri workflow answer T-3/step/1 --grant 1     (at a terminal; forwarded to the child)")]
    [InlineData("parked-budget", "budget", "sindri workflow answer T-3/step/1 --grant-tokens 100k     (at a terminal; once per park)")]
    [InlineData("parked-needs-answer-step", "needs-answer", "sindri workflow answer T-3/step/1 --text <answer>")]
    [InlineData("parked-needs-answer-wait", "needs-answer", "sindri workflow answer T-3/step/1 --resume [--text <answer>]")]
    [InlineData("parked-timeout", "timeout", "sindri workflow answer T-3/step/1 --resume")]
    [InlineData("parked-no-runtime", "SND-WF-010", "sindri workflow answer T-3/step/1 --resume")]
    [InlineData("escalated-gate", "gate", "sindri workflow answer T-3/step/1 --accept     (at a terminal)")]
    public void Each_park_reason_has_its_detail_and_answer_line(string name, string detail, string answer)
    {
        var view = StatusCases.View(name);

        Assert.Equal((detail, answer), (view.Detail, view.Answer));
        Assert.StartsWith("answer", StatusLayout.Commands(view), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("parked-revoked", "spec-conflict (revoked)", "cancel", "cancel")]
    [InlineData("parked-drift", "spec-conflict (drift)", "approve", "takeover, cancel")]
    public void A_spec_conflict_park_takes_no_answer_and_names_its_way_out(string name, string detail, string way, string commands)
    {
        var view = StatusCases.View(name);

        Assert.Equal(detail, view.Detail);
        Assert.Equal(commands, StatusLayout.Commands(view));   // spec 6.4: a revoked park offers only cancel (PR 232 proof F4)
        Assert.Contains(way, view.Answer, StringComparison.Ordinal);
    }

    /// <summary>PR 232 proof F2: a dispatch park's reason already ends in its own fix, so the status says it once and adds the allowlist sentence only for SND-WF-011.</summary>
    [Theory]
    [InlineData("parked-dispatch-no-runtime", false)]
    [InlineData("parked-dispatch-effect-failed", false)]
    [InlineData("parked-dispatch-allowlist", true)]
    public void A_dispatch_park_says_its_reason_and_its_fix_once(string name, bool allowlist)
    {
        var view = StatusCases.View(name);
        var next = view.Next;

        Assert.Contains(allowlist ? "once the profile allows the effect" : "once the cause is fixed", view.Answer, StringComparison.Ordinal);   // rc2-1: the answer line follows the refusal like Next does
        Assert.Equal(1, next.Split("resume to send it again", StringSplitOptions.None).Length - 1);
        Assert.Equal(allowlist ? 1 : 0, next.Split("isn't on the profile's effect allowlist", StringSplitOptions.None).Length - 1);
        Assert.StartsWith("effect.claim.ref ", next, StringComparison.Ordinal);
    }

    [Fact]
    public void A_drift_behind_a_park_names_the_block_and_the_reapproval_first()
    {
        var view = StatusCases.View("parked-rounds-with-drift");

        Assert.Equal(
            "step.produce changed since workflow.step@" + Hash12() + " was approved; answer after the re-approval: sindri recipe approve workflow.step@" + Hash12(),
            view.Drift);
    }

    [Fact]
    public void A_drift_park_that_the_reapproval_already_ended_is_no_drift_note_and_no_drift_line()
    {
        var view = StatusCases.View("parked-rounds-with-approved-drift");

        Assert.Null(view.Drift);
        Assert.Null(view.DriftNote);
    }

    [Fact]
    public void A_drift_park_queued_behind_another_park_gives_the_same_drift_line_and_note()
    {
        var view = StatusCases.View("parked-rounds-with-later-drift");

        Assert.NotNull(view.Drift);
        Assert.Equal(("step.produce", true), (view.DriftNote!.Block.Value, view.DriftNote.NeedsReapproval));
    }

    [Fact]
    public void An_ended_instance_has_its_outcome_as_detail_and_nothing_else()
    {
        var view = StatusCases.View("done");

        Assert.Equal(("done", "resolved", string.Empty), (view.State, view.Detail, view.Next));
        Assert.Empty(view.AllowedCommands);
        Assert.Null(view.Budget);
        Assert.Null(view.Answer);
    }

    private static string Hash12() => StatusCases.Recipe.Version.Hash.Value["sha256:".Length..][..12];
}

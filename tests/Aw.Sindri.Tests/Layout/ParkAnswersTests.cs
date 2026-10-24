using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Tests.GetInstance;

namespace Aw.Sindri.Tests.Layout;

/// <summary>The answer a park takes is one rule, shared by the status layout and the needs-you rows (API P11 U3).</summary>
[Lane(TestLane.Unit)]
public sealed class ParkAnswersTests
{
    private static readonly InstanceAddress _address = InstanceAddress.From("T-3/step/1");

    [Theory]
    [InlineData("parked-rounds", SuspensionKind.Park, ParkReason.Rounds, ParkKind.Loop, null)]
    [InlineData("parked-no-progress-child", SuspensionKind.Park, ParkReason.NoProgress, ParkKind.Loop, null)]
    [InlineData("parked-budget", SuspensionKind.Park, ParkReason.Budget, ParkKind.Step, null)]
    [InlineData("parked-needs-answer-step", SuspensionKind.Park, ParkReason.NeedsAnswer, ParkKind.Step, null)]
    [InlineData("parked-needs-answer-wait", SuspensionKind.Park, ParkReason.NeedsAnswer, ParkKind.Wait, null)]
    [InlineData("parked-timeout", SuspensionKind.Park, ParkReason.Timeout, null, null)]
    [InlineData("parked-revoked", SuspensionKind.Park, ParkReason.SpecConflict, ParkKind.Revoked, null)]
    [InlineData("parked-drift", SuspensionKind.Park, ParkReason.SpecConflict, ParkKind.Drift, null)]
    [InlineData("parked-no-runtime", SuspensionKind.Park, ParkReason.NoRuntime, null, null)]
    [InlineData("escalated-gate", SuspensionKind.Escalation, null, null, EscalationKind.Gate)]
    public void Park_answers_equal_the_status_answer_line_for_every_layout(string layout, SuspensionKind kind, ParkReason? reason, ParkKind? parkKind, EscalationKind? escalation)
    {
        var expected = StatusCases.View(layout).AnswerLine;

        var answer = ParkAnswers.For(new ParkShape(kind, reason, parkKind, escalation), _address, grantTokens: "5M");

        Assert.Equal(expected, answer);
    }

    [Theory]
    [InlineData(ParkReason.NeedsAnswer, null, "--text <answer>", AnswerKind.Text)]
    [InlineData(ParkReason.NeedsAnswer, ParkKind.Wait, "--resume [--text <answer>]", AnswerKind.Resume)]
    [InlineData(ParkReason.Budget, null, "--grant-tokens <n>", AnswerKind.GrantTokens)]
    public void A_mirrored_park_whose_reason_gives_the_childs_answer_names_the_childs_own_command_via_the_child(ParkReason reason, ParkKind? parkKind, string args, AnswerKind kind)
    {
        var answer = ParkAnswers.For(new ParkShape(SuspensionKind.Park, reason, ParkKind: null, Escalation: null, Mirrors: true, MirroredParkKind: parkKind), _address, grantTokens: "1M", child: "T-3/step/1/step.4")!;

        Assert.Equal(($"sindri workflow answer T-3/step/1/step.4 {args}", kind, "child", "T-3/step/1/step.4"), (answer.Cli, answer.Kind, answer.Via, answer.Child));
    }

    [Fact]
    public void A_mirrored_spec_conflict_stays_a_pointer_because_the_childs_park_kind_is_not_known()
    {
        var answer = ParkAnswers.For(new ParkShape(SuspensionKind.Park, ParkReason.SpecConflict, ParkKind: null, Escalation: null, Mirrors: true), _address, child: "T-3/step/1/step.4")!;

        Assert.Equal(("sindri workflow answer T-3/step/1 <the child's answer>", "child"), (answer.Cli, answer.Via));
    }

    [Fact]
    public void A_takeover_takes_no_answer() =>
        Assert.Null(ParkAnswers.For(new ParkShape(SuspensionKind.Takeover, Reason: null, ParkKind: null, Escalation: null), _address));
}

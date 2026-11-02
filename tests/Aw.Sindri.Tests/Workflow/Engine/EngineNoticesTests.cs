using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Ids;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// The engine's own notices (engine spec §5.7 origin 'engine', §6.3 notifications; engine P6 unit (c)): parks,
/// escalations and takeoverIdle interrupt; an instance's first desk task interrupts once and later ones are info. Each
/// is one 'effect.notify.desk' with identity '{instance}:{entry}:n{version}'.
/// </summary>
public sealed class EngineNoticesTests
{
    private static readonly BlockId _notify = BlockId.From("effect.notify.desk");

    [Fact, Trait("Lane", "unit")]
    public void A_park_sends_one_interrupt_notice()
    {
        var open = At(Step, "produce");
        var parked = Suspensions.Suspend(open, Park(ParkReason.Timeout, "The desk task timed out."));

        var (command, kind) = Assert.Single(EngineNotices.For(open, parked, version: 4, deskTaskNotified: true));

        Assert.Equal(NoticeClass.Interrupt, kind);
        Assert.Equal((_notify, CommandOrigin.Engine), (command.Effect, command.Origin));
        Assert.Equal(EffectIdentity.ForNotice(Id, 1, 4), command.Identity);
        Assert.Equal($"{Id.Value}:1:n4", command.Identity.Text);
        Assert.Contains("timed out", Text(command), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_escalation_sends_an_interrupt_notice()
    {
        var open = At(Step, "produce");
        var escalated = Suspensions.Suspend(open, Escalation("A gate escalated."));

        var (_, kind) = Assert.Single(EngineNotices.For(open, escalated, version: 3, deskTaskNotified: true));

        Assert.Equal(NoticeClass.Interrupt, kind);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_park_that_stays_parked_sends_nothing_again()
    {
        var parked = Suspensions.Suspend(At(Step, "produce"), Park(ParkReason.Timeout, "The desk task timed out."));

        Assert.Empty(EngineNotices.For(parked, parked with { Facts = parked.Facts }, version: 5, deskTaskNotified: true));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_first_desk_task_interrupts_and_a_later_one_is_info()
    {
        var first = At(Step, "produce");
        var second = At(Step, "produce", entry: 2);

        var (start, startKind) = Assert.Single(EngineNotices.For(before: null, first, version: 1, deskTaskNotified: false));
        var (_, laterKind) = Assert.Single(EngineNotices.For(first, second, version: 2, deskTaskNotified: true));

        Assert.Equal((NoticeClass.Interrupt, NoticeClass.Info), (startKind, laterKind));
        Assert.Equal($"{Id.Value}:1:n1", start.Identity.Text);
        Assert.Contains("waiting for you", Text(start), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unavailable_park_notifies_no_one_because_it_asks_nothing_of_a_human()
    {
        var open = At(Step, "produce");
        var parked = Suspensions.Suspend(open, Park(ParkReason.Unavailable, "step.produce is unavailable after 2 retries; the last error: exit"));

        Assert.Empty(EngineNotices.For(open, parked, version: 4, deskTaskNotified: true));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_resume_into_the_same_desk_run_sends_nothing()
    {
        var open = At(Step, "produce");
        var parked = Suspensions.Suspend(open, Park(ParkReason.NeedsAnswer, "Which branch?"));

        Assert.Empty(EngineNotices.For(parked, open, version: 6, deskTaskNotified: true));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_takeover_idle_notice_interrupts_and_asks_to_hand_back()
    {
        var taken = Suspensions.TakeOver(At(Step, "produce"), "human:thor").Value;

        var (command, kind) = Assert.Single(EngineNotices.For(taken, taken, version: 7, deskTaskNotified: true, WorkflowEventType.Notified));

        Assert.Equal(NoticeClass.Interrupt, kind);
        Assert.Contains("Hand it back or cancel it", Text(command), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_takeover_itself_and_an_ended_instance_send_nothing()
    {
        var open = At(Step, "produce");
        var taken = Suspensions.TakeOver(open, "human:thor").Value;

        Assert.Empty(EngineNotices.For(open, taken, version: 2, deskTaskNotified: true));
        Assert.Empty(EngineNotices.For(open, Suspensions.Cancel(open), version: 2, deskTaskNotified: false));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_park_names_the_park_row_an_escalation_too_a_desk_task_the_desk_row_and_a_takeover_the_takeover_row()
    {
        var open = At(Step, "produce");

        var park = Assert.Single(EngineNotices.For(open, Suspensions.Suspend(open, Park(ParkReason.Timeout, "q")), version: 4, deskTaskNotified: true)).Command;
        var escalation = Assert.Single(EngineNotices.For(open, Suspensions.Suspend(open, Escalation("q")), version: 4, deskTaskNotified: true)).Command;
        var desk = Assert.Single(EngineNotices.For(before: null, open, version: 1, deskTaskNotified: false)).Command;
        var taken = Suspensions.TakeOver(open, "human:thor").Value;
        var takeover = Assert.Single(EngineNotices.For(taken, taken, version: 7, deskTaskNotified: true, WorkflowEventType.Notified)).Command;

        Assert.Equal(("park", "park", "desk", "takeover"), (Row(park), Row(escalation), Row(desk), Row(takeover)));
    }

    private static string Row(EffectCommand command) => command.With["row"].Literal!.Value.GetString()!;   // why: an engine notice always names its row

    private static string Text(EffectCommand command) => command.With["text"].Literal!.Value.GetString()!;   // why: an engine notice always carries its text

    private static Suspension Park(ParkReason reason, string question) =>
        new(SuspensionKind.Park, reason, ParkKind: null, Escalation: null, question, "system:timer", Mirrors: null, new ResumeAction(ResumeKind.Rearm, Withheld: null, Gate: null, Effects: []));

    private static Suspension Escalation(string question) =>
        new(SuspensionKind.Escalation, Reason: null, ParkKind: null, EscalationKind.Gate, question, "system:gate", Mirrors: null, new ResumeAction(ResumeKind.Reevaluate, Withheld: null, Gate: null, Effects: []));
}

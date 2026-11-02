using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// What the owner reads in a desktop banner (GitHub #1111): the stage in plain words and what to do, never the instance address,
/// an item id, a run token, a step id or a park reason code. The raw address stays in notifications.jsonl (DeskNotice.Instance).
/// </summary>
public sealed class EngineNoticeWordsTests
{
    [Fact, Trait("Lane", "unit")]
    public void Park_banner_names_the_stage_and_no_internal_ids_or_reason_codes()
    {
        var open = At(Step, "produce");
        var parked = Suspensions.Suspend(open, Park(ParkReason.NeedsAnswer, "step.produce failed: lost"));

        var text = Text(Assert.Single(EngineNotices.For(open, parked, version: 4, deskTaskNotified: true)).Command);

        Assert.Contains("produce", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(parked.Address.Value, text, StringComparison.Ordinal);
        Assert.DoesNotContain("needs-answer", text, StringComparison.Ordinal);
        Assert.DoesNotContain("step.produce", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Desk_task_banner_names_the_stage_and_no_address_or_run_token()
    {
        var open = At(Step, "produce");

        var text = Text(Assert.Single(EngineNotices.For(before: null, open, version: 1, deskTaskNotified: false)).Command);

        Assert.Contains("produce", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(open.Address.Value, text, StringComparison.Ordinal);
        Assert.DoesNotContain("run ", text, StringComparison.Ordinal);
        Assert.DoesNotContain("desk task", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Takeover_idle_banner_has_no_address()
    {
        var taken = Suspensions.TakeOver(At(Step, "produce"), "human:thor").Value;

        var text = Text(Assert.Single(EngineNotices.For(taken, taken, version: 7, deskTaskNotified: true, WorkflowEventType.Notified)).Command);

        Assert.DoesNotContain(taken.Address.Value, text, StringComparison.Ordinal);
        Assert.DoesNotContain(taken.Item.Value, text, StringComparison.Ordinal);
    }

    private static string Text(EffectCommand command) => command.With["text"].Literal!.Value.GetString()!;   // why: an engine notice always carries its text

    private static Suspension Park(ParkReason reason, string question) =>
        new(SuspensionKind.Park, reason, ParkKind: null, Escalation: null, question, "system:timer", Mirrors: null, new ResumeAction(ResumeKind.Rearm, Withheld: null, Gate: null, Effects: []));
}

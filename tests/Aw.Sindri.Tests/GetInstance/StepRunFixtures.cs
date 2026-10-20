using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>Workflow ledger events as the engine mirrors them, for the step-run fold.</summary>
internal static class StepRunFixtures
{
    private const string Id = "WFI-00000000000000000000000000000001";
    private static readonly DateTimeOffset _t0 = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The time <paramref name="minutes"/> after the first event.</summary>
    public static DateTimeOffset At(int minutes) => _t0.AddMinutes(minutes);

    /// <summary>A start (or any transition) that enters <paramref name="step"/> and opens run <paramref name="token"/>.</summary>
    public static RecordedWorkflowEvent Started(string step, string token, int minutes = 0, StepRuntime runtime = StepRuntime.Agent, string from = "", string type = "workflow.started") =>
        new(type, At(minutes), "service:workflow", new WorkflowEventPayload(Id, from, step, "start", Outcome: null, Run: null, Detail("service:workflow", Frame(step, token, runtime))));

    /// <summary>A report on <paramref name="token"/>; it leaves <paramref name="step"/> for <paramref name="to"/>, opening <paramref name="next"/> there when given.</summary>
    public static RecordedWorkflowEvent Completed(string step, string to, string token, string? outcome, int minutes = 5, int? tokens = null, string actor = "agent:session", string? next = null, string kind = "step.completed", string type = "workflow.transition")
    {
        var entry = int.Parse(token[..token.IndexOf('.', StringComparison.Ordinal)], System.Globalization.CultureInfo.InvariantCulture);
        var frame = next is null ? new FrameSummary(to, Entry: entry + 1, Run: null, Child: null) : Frame(to, next, StepRuntime.Agent);
        return new(type, At(minutes), "service:workflow", new WorkflowEventPayload(Id, step, to, kind, outcome, token, Detail(actor, frame, tokens)));
    }

    /// <summary>A cancel that ends the instance in 'cancelled'.</summary>
    public static RecordedWorkflowEvent Cancelled(string from, int minutes, string actor = "human:owner") =>
        new("workflow.transition", At(minutes), "service:workflow", new WorkflowEventPayload(Id, from, "cancelled", "cancel", Outcome: null, Run: null, Detail(actor, new FrameSummary("cancelled", Entry: 9, Run: null, Child: null))));

    /// <summary>A transition that leaves a state with a frame that has no open run, without a report.</summary>
    public static RecordedWorkflowEvent Moved(string from, string to, int minutes, string kind = "answer") =>
        new("workflow.transition", At(minutes), "service:workflow", new WorkflowEventPayload(Id, from, to, kind, Outcome: null, Run: null, Detail("human:owner", new FrameSummary(to, Entry: 9, Run: null, Child: null))));

    /// <summary>A park: the frame stays as it was (the delta carries none).</summary>
    public static RecordedWorkflowEvent Parked(string state, int minutes, string kind = "deadline") =>
        new("workflow.transition", At(minutes), "service:workflow", new WorkflowEventPayload(Id, state, state, kind, Outcome: null, Run: null, Detail("service:timer", frame: null)));

    private static FrameSummary Frame(string step, string token, StepRuntime runtime) =>
        new(step, int.Parse(token[..token.IndexOf('.', StringComparison.Ordinal)], System.Globalization.CultureInfo.InvariantCulture), token, Child: null, runtime);

    private static WorkflowEventDetail Detail(string actor, FrameSummary? frame, int? tokens = null) =>
        new(TriggerKey.For(WorkflowInstanceId.From(Id), "t"), actor, SourceSequence: null, ArgumentsSha256: null, ReportSha256: null, Answer: null, Amount: null, Reason: null,
            Gates: [], Evidence: [], Grant: null, Override: null, Refusal: null, Effects: [],
            new WorkflowEventDelta(frame, Suspension: null, PendingPark: null, Loops: null, TokensSpent: null, TokensGranted: null, Facts: null, Outcome: null))
        {
            Tokens = tokens,
        };
}

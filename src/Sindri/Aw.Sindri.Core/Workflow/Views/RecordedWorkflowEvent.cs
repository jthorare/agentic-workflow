namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>
/// An instance's workflow ledger event, as the step-run fold reads it: plain values only, mapped from the ledger row by
/// the reader, so the fold depends on no event payload type.
/// </summary>
/// <param name="Type">The ledger event type ('workflow.started', 'workflow.transition', 'workflow.refused', …).</param>
/// <param name="At">When the ledger recorded it.</param>
/// <param name="Actor">The envelope's actor, the fallback when the row names no trigger actor.</param>
/// <param name="Trigger">The trigger that caused the change ('step.completed', 'cancel', …).</param>
/// <param name="Outcome">The outcome the trigger reported, if any.</param>
/// <param name="Run">The run token the trigger carried, if any.</param>
/// <param name="Tokens">The tokens the row recorded for the run, if any.</param>
/// <param name="TriggerActor">Who fired the trigger, if the row says.</param>
/// <param name="Frame">The frame the change left the instance in; null when the frame was not touched.</param>
public sealed record RecordedWorkflowEvent(string Type, DateTimeOffset At, string Actor, string Trigger, string? Outcome, string? Run, int? Tokens, string? TriggerActor, RecordedFrame? Frame)
{
    /// <summary>How many images a judge choice looked at; null for any other event.</summary>
    public int? ImageCount { get; init; }

    /// <summary>How many images a judge choice left out; null when none were.</summary>
    public int? ImagesDropped { get; init; }
}

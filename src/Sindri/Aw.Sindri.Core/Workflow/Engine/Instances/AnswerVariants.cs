using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Core.Workflow.Engine.Instances;

/// <summary>
/// Which answer variants fit a suspension (spec §3.8): the one table the answer check (Rules.FitRow) and the served
/// commands (Runtime.InstanceViews) share, so what is offered is what is accepted.
/// </summary>
internal static class AnswerVariants
{
    /// <summary>The answer variants that fit a suspension, in the order spec §3.8 lists them (the status line's own answer first).</summary>
    /// <param name="suspension">The park or escalation (a mirror's reason or escalation as it carries them).</param>
    /// <returns>The variants; empty when only cancel or an approval ends it.</returns>
    internal static IReadOnlyList<AnswerVariant> Fitting(Suspension suspension)
    {
        ArgumentNullException.ThrowIfNull(suspension);
        return suspension.Kind == SuspensionKind.Escalation
            ? suspension.Escalation switch
            {
                EscalationKind.Gate => [AnswerVariant.Accept, AnswerVariant.Reject],
                EscalationKind.Timeout => [AnswerVariant.Resume],
                EscalationKind.Child or null or _ => [],
            }
            : suspension.Reason switch
            {
                ParkReason.Rounds => [AnswerVariant.Grant],
                ParkReason.NoProgress => [AnswerVariant.Grant, AnswerVariant.Text],
                ParkReason.Budget => [AnswerVariant.GrantTokens],
                ParkReason.NeedsAnswer => suspension.ParkKind == ParkKind.Wait ? [AnswerVariant.Resume] : [AnswerVariant.Text],
                ParkReason.Timeout or ParkReason.NoRuntime or ParkReason.Launch => [AnswerVariant.Resume],
                ParkReason.SpecConflict or _ => suspension.ParkKind == ParkKind.Dispatch ? [AnswerVariant.Resume] : [],
            };
    }

    /// <summary>
    /// Whether the reason alone fixes which variants fit, whatever the child's park kind or budget: a rounds, no-progress,
    /// timeout or no-runtime park. A mirrored needs-answer (a wait or a Step), budget (the child's own cap) or
    /// spec-conflict park does not, so a suspension mirroring one offers no answer entry and points its status line at the child.
    /// </summary>
    /// <param name="reason">The park reason.</param>
    /// <returns>Whether the fit is the reason's alone.</returns>
    internal static bool ReasonFixesChildFit(ParkReason? reason) => reason is ParkReason.Rounds or ParkReason.NoProgress or ParkReason.Timeout or ParkReason.NoRuntime or ParkReason.Launch;
}

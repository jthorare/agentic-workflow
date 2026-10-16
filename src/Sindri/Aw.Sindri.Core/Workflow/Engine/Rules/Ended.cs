using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>A suspension that ended (spec §5.8): the instance after it, and the suspension whose resume action runs next.</summary>
/// <param name="Instance">The instance after it: the recipe frame as it was, and a pending park, if there was one, now the suspension.</param>
/// <param name="Suspension">What ended, with its resume action.</param>
/// <param name="PendingTookEffect">Whether a pending park took effect, so the resume action doesn't run.</param>
/// <param name="Released">The effects of every approved drift park (queued behind the one that ended, or approved while the instance was taken over), now free to be re-sent because nothing else holds the instance; the caller sends them (TransitionDecision.WithReleased). Null otherwise.</param>
internal sealed record Ended(WorkflowInstance Instance, Suspension Suspension, bool PendingTookEffect, IReadOnlyList<RefusedEffect>? Released = null);

using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>The parent after a child's change released it, and the effects its ended parks free to be sent (null when none).</summary>
/// <param name="Instance">The parent with nothing mirroring the child.</param>
/// <param name="Released">The effects the caller must send; see <see cref="Suspensions.EndCurrent"/>.</param>
internal sealed record ParentReleased(WorkflowInstance Instance, IReadOnlyList<RefusedEffect>? Released = null);

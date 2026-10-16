using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>
/// Ends the parent's suspension that mirrors a child (spec §5.8: any later change from the child pops that mirror
/// first), and drops a pending park that mirrors it. A park that waited behind the mirror takes effect; the suspension
/// ends by <see cref="Suspensions.EndCurrent"/>, like every other.
/// </summary>
internal static class ChildRelease
{
    /// <summary>See <see cref="ChildPropagation.Release"/>; the same instance when nothing mirrors <paramref name="child"/>.</summary>
    /// <param name="parent">The parent.</param>
    /// <param name="child">The child the change is from.</param>
    /// <returns>The parent with nothing mirroring that child, and the effects to send.</returns>
    public static ParentReleased Release(WorkflowInstance parent, WorkflowInstanceId child)
    {
        ArgumentNullException.ThrowIfNull(parent);
        var queue = Suspensions.PendingParks(parent);
        var kept = queue.Where(p => p.Mirrors != child).ToList();
        var released = kept.Count == queue.Count ? parent : Suspensions.WithPending(parent, kept);
        if (parent.Suspension?.Mirrors != child)
        {
            return new ParentReleased(released);
        }

        var ended = Suspensions.EndCurrent(released);
        return new ParentReleased(ended.Instance, ended.Released);
    }
}

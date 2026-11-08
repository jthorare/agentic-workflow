using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>The child reached a terminal state or was cancelled (spec §5.8): the parent's 'wait.satisfied' with that outcome.</summary>
/// <param name="Child">The child instance.</param>
/// <param name="ChildEventKey">The key of the child's event that ended it.</param>
public sealed record ChildWaitSource(WorkflowInstanceId Child, TriggerKey ChildEventKey) : WaitSource
{
    /// <inheritdoc/>
    public override string KeyPart => "child:" + ChildEventKey.Value;
}

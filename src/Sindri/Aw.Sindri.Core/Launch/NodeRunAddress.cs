using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Launch;

/// <summary>
/// Which node run a launch belongs to (PR 7): the workflow, the run, the node and the item's position inside a 'for_each' (0 for a node that runs once).
/// It plays the part a Step run's instance and token played. A run id holds '@' and ':', so the session name and the git branch never use it: they come from
/// <see cref="Launch"/> and <see cref="Branch"/>.
/// </summary>
/// <param name="Workflow">The workflow's slug.</param>
/// <param name="Run">The run's id.</param>
/// <param name="Node">The node.</param>
/// <param name="ItemIndex">The item's position, 0 for a node that runs once.</param>
public sealed record NodeRunAddress(WorkflowSlug Workflow, WorkflowRunId Run, NodeId Node, int ItemIndex)
{
    /// <summary>The address of a node run; the workflow is the run id's first part.</summary>
    /// <param name="run">The run.</param>
    /// <param name="node">The node.</param>
    /// <param name="itemIndex">The item's position, 0 for a node that runs once.</param>
    /// <returns>The address.</returns>
    public static NodeRunAddress Of(WorkflowRunId run, NodeId node, int itemIndex = 0) => throw new NotSupportedException("red");

    /// <summary>The launch id: 'LCH-' and the first 32 hex digits of SHA-256 of 'agent-launch:&lt;run&gt;:&lt;node&gt;:&lt;index&gt;'. A retry or a replay can never start a second session.</summary>
    public LaunchId Launch => ItemIndex < 0 ? LaunchId.From("LCH-00000000000000000000000000000000") : throw new NotSupportedException("red");

    /// <summary>The run's short name inside a branch: '&lt;revision&gt;-&lt;occurrence&gt;-&lt;node&gt;', and '-&lt;index&gt;' after it for an item of a 'for_each'. No ':' and no '@'.</summary>
    public string ShortRun => ItemIndex < 0 ? string.Empty : throw new NotSupportedException("red");

    /// <summary>The branch the node's worktree is made on: 'wf/&lt;slug&gt;/&lt;short run&gt;'.</summary>
    public string Branch => ItemIndex < 0 ? string.Empty : throw new NotSupportedException("red");

    /// <summary>The branch namespace the session may write refs in: 'wf/&lt;slug&gt;'. <see cref="Branch"/> sits inside it.</summary>
    public string RefNamespace => ItemIndex < 0 ? string.Empty : throw new NotSupportedException("red");
}

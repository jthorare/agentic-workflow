using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Launch;

/// <summary>
/// Which node run a launch belongs to (PR 7): the workflow, the run, the node and the item's position inside a 'for_each' (0 for a node that runs once).
/// It plays the part a Step run's instance and token played. A run id holds '@' and ':', so the session name and the git branch never use it: they come from
/// <see cref="Launch()"/> and <see cref="Branch"/>.
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
    public static NodeRunAddress Of(WorkflowRunId run, NodeId node, int itemIndex = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(itemIndex);
        return new NodeRunAddress(WorkflowSlug.From(run.Value[..run.Value.IndexOf('@', StringComparison.Ordinal)]), run, node, itemIndex);
    }

    /// <summary>The launch id: 'LCH-' and the first 32 hex digits of SHA-256 of 'agent-launch:&lt;run&gt;:&lt;node&gt;:&lt;index&gt;'. A retry or a replay can never start a second session.</summary>
    /// <returns>The launch id.</returns>
    /// <remarks>A method, not a property: ARCH-ID-002 asks every typed-id property to be required on read, and this one is computed.</remarks>
    public LaunchId Launch()
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"agent-launch:{Run.Value}:{Node.Value}:{ItemIndex}")));
        return LaunchId.From("LCH-" + Convert.ToHexStringLower(digest)[..32]);
    }

    /// <summary>The run's short name inside a branch: '&lt;revision&gt;-&lt;occurrence&gt;-&lt;node&gt;', and '-&lt;index&gt;' after it for an item of a 'for_each'. No ':' and no '@'.</summary>
    public string ShortRun
    {
        get
        {
            var at = Run.Value.IndexOf('@', StringComparison.Ordinal);
            var colon = Run.Value.IndexOf(':', StringComparison.Ordinal);
            var item = ItemIndex > 0 ? string.Create(CultureInfo.InvariantCulture, $"-{ItemIndex}") : string.Empty;
            return $"{Run.Value[(at + 1)..colon]}-{Run.Value[(colon + 1)..]}-{Node.Value}{item}";   // why: two nodes of one run need two branches
        }
    }

    /// <summary>The node's own branch name: 'wf/&lt;slug&gt;/&lt;short run&gt;'. The sandbox lets the session write refs under it and nowhere else.</summary>
    public string Branch => $"wf/{Workflow.Value}/{ShortRun}";

    /// <summary>The branch namespace the session may write refs in: the node's own <see cref="Branch"/>. The sandbox makes it a ref directory, so the checked-out branch is <see cref="WorkBranch"/>, one level inside it (as an attended open's is).</summary>
    public string RefNamespace => Branch;

    /// <summary>The branch the node's worktree is made on: '&lt;branch&gt;/work'. Another node's, another run's and the workflow's other branches are outside the session's reach.</summary>
    public string WorkBranch => Branch + "/work";
}

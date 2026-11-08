using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>Names derived from a run for its agent session. A run id holds '@' and ':', which a git ref and a tmux session name can't, so they never carry it.</summary>
public static class RunAgentNames
{
    /// <summary>The branch a node's session works on: 'wf/&lt;slug&gt;/r&lt;revision&gt;-&lt;occurrence&gt;' ('wf/contract-drift/r3-12' for 'contract-drift@3:12').</summary>
    /// <param name="run">The run.</param>
    /// <returns>The branch name.</returns>
    public static string Branch(WorkflowRunId run)
    {
        var id = run.Value;
        var at = id.IndexOf('@', StringComparison.Ordinal);
        var colon = id.IndexOf(':', StringComparison.Ordinal);
        return $"wf/{id[..at]}/r{id[(at + 1)..colon]}-{id[(colon + 1)..]}";
    }
}

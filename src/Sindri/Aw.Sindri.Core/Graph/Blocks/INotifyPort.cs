using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>Where a 'notify' node's message goes: a row on the Needs-you page.</summary>
public interface INotifyPort
{
    /// <summary>Writes the row.</summary>
    /// <param name="runId">The run that asked.</param>
    /// <param name="node">The node that asked.</param>
    /// <param name="title">The row's title.</param>
    /// <param name="body">The row's text, or null.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or why the row could not be written.</returns>
    public ValueTask<Result<Unit>> Post(WorkflowRunId runId, NodeId node, string title, string? body, CancellationToken cancellationToken);
}

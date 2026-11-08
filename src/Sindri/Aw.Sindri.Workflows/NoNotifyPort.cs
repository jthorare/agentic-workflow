using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The 'notify' port of a build whose host registers none: the node fails with the reason.</summary>
internal sealed class NoNotifyPort : INotifyPort
{
    /// <inheritdoc />
    public ValueTask<Result<Unit>> Post(WorkflowRunId runId, NodeId node, string title, string? body, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<Unit>>(SindriErrors.GraphBlockFailed.With("this build has no place to post a 'notify' node"));
}

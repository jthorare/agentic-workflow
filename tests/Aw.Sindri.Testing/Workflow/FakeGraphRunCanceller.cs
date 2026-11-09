using System.Collections.Concurrent;
using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>An <see cref="IGraphRunCanceller"/> that stops nothing and remembers which runs it was asked to cancel.</summary>
public sealed class FakeGraphRunCanceller : IGraphRunCanceller
{
    private readonly ConcurrentQueue<WorkflowRunId> _cancelled = new();

    /// <summary>The runs it was asked to cancel, in order.</summary>
    public IReadOnlyList<WorkflowRunId> Cancelled => [.. _cancelled];

    /// <summary>When set, every cancel fails with this error.</summary>
    public Error? Fail { get; set; }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Cancel(WorkflowRunId run, CancellationToken cancellationToken)
    {
        if (Fail is { } error)
        {
            return ValueTask.FromResult<Result<Unit>>(error);
        }

        _cancelled.Enqueue(run);
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }
}

using System.Collections.Concurrent;
using Aw.Kernel;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>An <see cref="IGraphRunLauncher"/> that starts nothing and remembers which runs it was asked to start.</summary>
public sealed class FakeGraphRunLauncher : IGraphRunLauncher
{
    private readonly ConcurrentQueue<WorkflowRun> _launched = new();

    /// <summary>The runs it was asked to start, in order.</summary>
    public IReadOnlyList<WorkflowRun> Launched => [.. _launched];

    /// <inheritdoc/>
    public ValueTask<Result<bool>> Launch(WorkflowRun run, CancellationToken cancellationToken)
    {
        _launched.Enqueue(run);
        return ValueTask.FromResult<Result<bool>>(true);
    }
}

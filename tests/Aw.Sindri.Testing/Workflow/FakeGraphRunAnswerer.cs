using System.Collections.Concurrent;
using Aw.Kernel;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>An <see cref="IGraphRunAnswerer"/> that delivers nothing and remembers the answers it was given.</summary>
public sealed class FakeGraphRunAnswerer : IGraphRunAnswerer
{
    private readonly ConcurrentQueue<GraphAnswer> _answers = new();

    /// <summary>The answers it was given, in order.</summary>
    public IReadOnlyList<GraphAnswer> Answers => [.. _answers];

    /// <summary>When set, every answer fails with this error.</summary>
    public Error? Fail { get; set; }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Answer(GraphAnswer answer, CancellationToken cancellationToken)
    {
        if (Fail is { } error)
        {
            return ValueTask.FromResult<Result<Unit>>(error);
        }

        _answers.Enqueue(answer);
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }
}

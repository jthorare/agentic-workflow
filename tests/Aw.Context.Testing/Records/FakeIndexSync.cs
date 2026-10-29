using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Kernel;

namespace Aw.Context.Testing.Records;

/// <summary>An <see cref="IIndexSync"/> that reports the progress rows a test sets and runs no feed.</summary>
public sealed class FakeIndexSync : IIndexSync
{
    /// <summary>What <see cref="Progress"/> answers.</summary>
    public IReadOnlyList<SourceProgress> Standing { get; set; } = [];

    /// <inheritdoc />
    public ValueTask<Result<SyncReport>> Tick(ContextScope scope, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<SyncReport>>(new SyncReport([]));

    /// <inheritdoc />
    public ValueTask<Result<DecisionReport>> Decide(ContextScope scope, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<DecisionReport>>(new DecisionReport([]));

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<SourceProgress>>> Progress(ContextScope scope, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<IReadOnlyList<SourceProgress>>>(Standing.ToList());

    /// <inheritdoc />
    public ValueTask<Result<SourceCounts>> Counts(SourceId source, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<SourceCounts>>(new SourceCounts(0, 0, 0));

    /// <inheritdoc />
    public ValueTask<Result<int>> Purge(SourceId source, CancellationToken cancellationToken) => ValueTask.FromResult<Result<int>>(0);
}

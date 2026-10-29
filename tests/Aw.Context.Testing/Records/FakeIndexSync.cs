using Aw.Context.Core.ConceptSynthesis;
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

    /// <summary>When set, <see cref="Progress"/> answers this error instead of <see cref="Standing"/>.</summary>
    public Error? ProgressFailure { get; set; }

    /// <inheritdoc />
    public ValueTask<Result<SyncReport>> Tick(ContextScope scope, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<SyncReport>>(new SyncReport([]));

    /// <inheritdoc />
    public ValueTask<Result<SyncReport>> Tick(ContextScope scope, SourceId source, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<SyncReport>>(new SyncReport([]));

    /// <inheritdoc />
    public ValueTask<Result<DecisionReport>> Decide(ContextScope scope, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<DecisionReport>>(new DecisionReport([]));

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<SourceProgress>>> Progress(ContextScope scope, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<IReadOnlyList<SourceProgress>>>(ProgressFailure is { } failure ? failure : Standing.ToList());

    /// <summary>What <see cref="ConceptStatus"/> answers.</summary>
    public ConceptStatus Concepts { get; set; } = new(Enabled: false, Concepts: 0, MultiSource: 0, Queued: 0, Covered: 0, Watermark: 0, Embedded: 0, Pending: 0);

    /// <inheritdoc />
    public ValueTask<Result<ConceptStatus>> ConceptStatus(ContextScope scope, CancellationToken cancellationToken) => ValueTask.FromResult<Result<ConceptStatus>>(Concepts);

    /// <inheritdoc />
    public ValueTask<Result<SourceCounts>> Counts(SourceId source, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<SourceCounts>>(new SourceCounts(0, 0, 0));

    /// <inheritdoc />
    public ValueTask<Result<int>> Purge(SourceId source, CancellationToken cancellationToken) => ValueTask.FromResult<Result<int>>(0);
}

using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Kernel;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>An <see cref="IIndexSync"/> that answers what a test sets: the tick's rows, each source's progress, and what a source holds; it records the ticks and the purges.</summary>
internal sealed class FakeIndexSync : IIndexSync
{
    public IReadOnlyList<SourceSync> Rows { get; set; } = [];

    public IReadOnlyList<SourceProgress> Standing { get; set; } = [];

    public SourceCounts Held { get; set; } = new(0, 0, 0);

    public IReadOnlyList<SourceDecisions> Decided { get; set; } = [];

    public int Ticks { get; private set; }

    public int Decides { get; private set; }

    public List<SourceId> Purged { get; } = [];

    public ValueTask<Result<SyncReport>> Tick(ContextScope scope, CancellationToken cancellationToken)
    {
        Ticks++;
        return ValueTask.FromResult<Result<SyncReport>>(new SyncReport(Rows));
    }

    public ValueTask<Result<DecisionReport>> Decide(ContextScope scope, CancellationToken cancellationToken)
    {
        Decides++;
        return ValueTask.FromResult<Result<DecisionReport>>(new DecisionReport(Decided));
    }

    public ValueTask<Result<IReadOnlyList<SourceProgress>>> Progress(ContextScope scope, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success(Standing));

    public ValueTask<Result<SourceCounts>> Counts(SourceId source, CancellationToken cancellationToken) => ValueTask.FromResult<Result<SourceCounts>>(Held);

    public ValueTask<Result<int>> Purge(SourceId source, CancellationToken cancellationToken)
    {
        Purged.Add(source);
        return ValueTask.FromResult<Result<int>>(Held.Records);
    }
}

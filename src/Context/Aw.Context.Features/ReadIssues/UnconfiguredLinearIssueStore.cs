using Aw.Context.Core;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Features.ReadIssues;

/// <summary>The Linear issue store of a host that wired none: every call says the store can't answer (SND-CONTEXT-004). Aw.Context.Infrastructure replaces it.</summary>
internal sealed class UnconfiguredLinearIssueStore : ILinearIssueStore
{
    public ValueTask<Result<LinearIssuePage>> List(LinearIssueFilter filter, LinearIssueCursor? after, RecordAudience audience, CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<LinearIssuePage>());

    public ValueTask<Result<LinearIssueRow?>> Get(RecordKey key, RecordAudience audience, CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<LinearIssueRow?>());

    public ValueTask<Result<LinearIssueDetail?>> Get(LinearIssueKey key, RecordAudience audience, CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<LinearIssueDetail?>());

    public ValueTask<Result<LinearIssueCounts>> Count(LinearIssueFilter filter, RecordAudience audience, CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<LinearIssueCounts>());

    public ValueTask<Result<IReadOnlyList<LinearCommentRow>>> Comments(RecordKey key, RecordAudience audience, CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<IReadOnlyList<LinearCommentRow>>());

    public ValueTask<Result<LinearSince>> Since(DateTimeOffset marker, ExternalId viewer, RecordAudience audience, CancellationToken cancellationToken) => ValueTask.FromResult(Unwired<LinearSince>());

    private static Result<T> Unwired<T>() =>
        ContextErrors.StoreUnavailable.With("no Linear issue store is wired in this host (fix: add Aw.Context.Infrastructure's registration)");
}

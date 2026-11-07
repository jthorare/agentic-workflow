using Aw.Context.Core.Linear;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Testing.Records;

/// <summary>An <see cref="ILinearIssueStore"/> that holds nothing: every list is empty and every issue unknown. For the engines of tests that never read Linear issues.</summary>
public sealed class EmptyLinearIssueStore : ILinearIssueStore
{
    /// <inheritdoc />
    public ValueTask<Result<LinearIssuePage>> List(LinearIssueFilter filter, LinearIssueCursor? after, RecordAudience audience, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<LinearIssuePage>>(new LinearIssuePage([], Next: null));

    /// <inheritdoc />
    public ValueTask<Result<LinearIssueRow?>> Get(RecordKey key, RecordAudience audience, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success<LinearIssueRow?>(value: null));

    /// <inheritdoc />
    public ValueTask<Result<LinearIssueRow?>> Get(LinearIssueKey key, RecordAudience audience, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success<LinearIssueRow?>(value: null));

    /// <inheritdoc />
    public ValueTask<Result<LinearIssueCounts>> Count(LinearIssueFilter filter, RecordAudience audience, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<LinearIssueCounts>>(new LinearIssueCounts(new Dictionary<string, int>(StringComparer.Ordinal), Total: 0));

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<LinearCommentRow>>> Comments(RecordKey key, RecordAudience audience, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success<IReadOnlyList<LinearCommentRow>>([]));

    /// <inheritdoc />
    public ValueTask<Result<LinearSince>> Since(DateTimeOffset marker, ExternalId viewer, RecordAudience audience, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<LinearSince>>(new LinearSince([], []));
}

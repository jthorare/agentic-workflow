using Aw.Kernel;
using Aw.Sindri.Ui.Api.Linear;

namespace Aw.Sindri.Ui.Api;

/// <summary>The Linear calls (<see cref="SindriApiClient"/>, continued; Linear hub spec §3).</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<LinearIssueList>> LinearIssues(Uri api, LinearIssueQuery query, CancellationToken cancellationToken) =>
        throw new NotSupportedException(LinearRoutes.IssueList(query)); // red: the stub until the real call lands

    /// <inheritdoc />
    public Task<Result<LinearIssueOne>> LinearIssue(Uri api, string key, CancellationToken cancellationToken) =>
        throw new NotSupportedException(LinearRoutes.Issue(key)); // red: the stub until the real call lands

    /// <inheritdoc />
    public Task<Result<LinearLookups>> LinearLookups(Uri api, string team, CancellationToken cancellationToken) =>
        throw new NotSupportedException(LinearRoutes.Lookups(team)); // red: the stub until the real call lands

    /// <inheritdoc />
    public Task<Result<LinearSince>> LinearSince(Uri api, string? after, CancellationToken cancellationToken) =>
        throw new NotSupportedException(LinearRoutes.Since(after)); // red: the stub until the real call lands
}

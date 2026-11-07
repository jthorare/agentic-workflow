using Aw.Kernel;
using Aw.Sindri.Ui.Api.Linear;

namespace Aw.Sindri.Ui.Api;

/// <summary>The Linear calls (<see cref="SindriApiClient"/>, continued; Linear hub spec §3).</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<LinearIssueList>> LinearIssues(Uri api, LinearIssueQuery query, CancellationToken cancellationToken) =>
        Get<LinearIssueList>(api, LinearRoutes.IssueList(query), cancellationToken);

    /// <inheritdoc />
    public Task<Result<LinearIssueOne>> LinearIssue(Uri api, string key, CancellationToken cancellationToken) =>
        Get<LinearIssueOne>(api, LinearRoutes.Issue(key), cancellationToken);

    /// <inheritdoc />
    public Task<Result<LinearLookups>> LinearLookups(Uri api, string team, CancellationToken cancellationToken) =>
        Get<LinearLookups>(api, LinearRoutes.Lookups(team), cancellationToken);

    /// <inheritdoc />
    public Task<Result<LinearSince>> LinearSince(Uri api, string? after, CancellationToken cancellationToken) =>
        Get<LinearSince>(api, LinearRoutes.Since(after), cancellationToken);
}

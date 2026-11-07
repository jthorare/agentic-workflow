using System.Text.Json.Nodes;
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

    /// <inheritdoc />
    public Task<Result<LinearWriteOutcome>> LinearChange(Uri api, string key, JsonObject opened, JsonObject set, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<LinearWriteOutcome>> LinearComment(Uri api, string key, string clientId, string body, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<LinearWriteOutcome>> LinearCreate(Uri api, LinearCreateBody body, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<LinearConnection>> LinearConnection(Uri api, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<LinearTeamChoices>> LinearConnectionTeams(Uri api, string key, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<LinearConnected>> LinearConnect(Uri api, string kind, string key, IReadOnlyList<string>? teams, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<LinearDisconnected>> LinearDisconnectWrite(Uri api, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

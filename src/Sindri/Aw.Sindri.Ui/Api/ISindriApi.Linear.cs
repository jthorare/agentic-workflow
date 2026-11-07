using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Ui.Api.Linear;

namespace Aw.Sindri.Ui.Api;

/// <summary>The Linear reads of the API (<see cref="ISindriApi"/>, continued; Linear hub spec §3). None is a view read: the answers carry no asOf.</summary>
internal partial interface ISindriApi
{
    /// <summary>GET /sindri/linear/issues (Linear hub spec §3): one page of the Work page's Linear list.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="query">The group and filters.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The page (connected false when no Linear source is set up), the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<LinearIssueList>> LinearIssues(Uri api, LinearIssueQuery query, CancellationToken cancellationToken);

    /// <summary>GET /sindri/linear/issues/{key} (Linear hub spec §3): one issue with its description and comments.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="key">The issue key, such as ENG-12.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The issue, SND-LINEAR-005 (not stored, or outside a session's teams), or SND-UI-002/003.</returns>
    public Task<Result<LinearIssueOne>> LinearIssue(Uri api, string key, CancellationToken cancellationToken);

    /// <summary>GET /sindri/linear/lookups/{team} (Linear hub spec §3): a team's states, members, labels and cycles.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="team">The team key.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The lookups (empty with syncedAt null before the first sync), SND-AUTH-002 (a session), or SND-UI-002/003.</returns>
    public Task<Result<LinearLookups>> LinearLookups(Uri api, string team, CancellationToken cancellationToken);

    /// <summary>GET /sindri/linear/since (Linear hub spec §3): what changed on the owner's issues since a marker.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="after">The marker an earlier answer returned; null for everything since the source's first watermark.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The changes and the next marker, SND-AUTH-002 (a session), or SND-UI-002/003.</returns>
    public Task<Result<LinearSince>> LinearSince(Uri api, string? after, CancellationToken cancellationToken);

    /// <summary>POST /sindri/linear/issues/{key}/change (Linear hub 1b contract §1): edit fields of one issue.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="key">The issue key, such as ENG-12.</param>
    /// <param name="opened">The values the sheet opened with, only the changed fields.</param>
    /// <param name="set">The new values for the same fields.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns><see cref="LinearChangeApplied"/>, <see cref="LinearChangeConflict"/> (409), <see cref="LinearWriteUnknown"/> (504 SND-LINEAR-011), <see cref="LinearWriteRateLimited"/> (429); any other refusal (002, 003, 005, 006, 004, 015, 010, SND-AUTH-002) or SND-UI-002/003 as the error.</returns>
    public Task<Result<LinearWriteOutcome>> LinearChange(Uri api, string key, JsonObject opened, JsonObject set, CancellationToken cancellationToken);

    /// <summary>POST /sindri/linear/issues/{key}/comments (Linear hub 1b contract §2): comment on one issue.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="key">The issue key.</param>
    /// <param name="clientId">The app's uuid for this comment; a retry sends the same one.</param>
    /// <param name="body">The text.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns><see cref="LinearCommentPosted"/>, <see cref="LinearWriteUnknown"/> (with the client id), <see cref="LinearWriteRateLimited"/>, or the refusal as the error.</returns>
    public Task<Result<LinearWriteOutcome>> LinearComment(Uri api, string key, string clientId, string body, CancellationToken cancellationToken);

    /// <summary>POST /sindri/linear/issues (Linear hub 1b contract §3): create an issue.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="body">The new issue's fields.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns><see cref="LinearCreated"/>, <see cref="LinearWriteUnknown"/> (with the client id), <see cref="LinearWriteRateLimited"/>, or the refusal as the error.</returns>
    public Task<Result<LinearWriteOutcome>> LinearCreate(Uri api, LinearCreateBody body, CancellationToken cancellationToken);

    /// <summary>GET /sindri/linear/connection (Linear hub 1b contract §4a): the keys and teams, for Settings.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The connection, SND-AUTH-002 (a session), or SND-UI-002/003.</returns>
    public Task<Result<LinearConnection>> LinearConnection(Uri api, CancellationToken cancellationToken);

    /// <summary>POST /sindri/linear/connection/teams (Linear hub 1b contract §4b): the teams a read key can see.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="key">The pasted read key.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The teams, SND-LINEAR-004 (key refused), SND-LINEAR-010 (Linear unreachable), SND-AUTH-002, or SND-UI-002/003.</returns>
    public Task<Result<LinearTeamChoices>> LinearConnectionTeams(Uri api, string key, CancellationToken cancellationToken);

    /// <summary>POST /sindri/linear/connection (Linear hub 1b contract §4c): save a read or a write key.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="kind">"read" or "write".</param>
    /// <param name="key">The key.</param>
    /// <param name="teams">The team keys to read, for a read key; null for a write key.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Saved (approved or waiting), SND-LINEAR-001/003/004/014, SND-AUTH-002, or SND-UI-002/003.</returns>
    public Task<Result<LinearConnected>> LinearConnect(Uri api, string kind, string key, IReadOnlyList<string>? teams, CancellationToken cancellationToken);

    /// <summary>DELETE /sindri/linear/connection/write (Linear hub 1b contract §4d): remove the write key.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Removed (approved or waiting), SND-AUTH-002, or SND-UI-002/003.</returns>
    public Task<Result<LinearDisconnected>> LinearDisconnectWrite(Uri api, CancellationToken cancellationToken);
}

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
}

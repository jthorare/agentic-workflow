using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>
/// 'GET /sindri/linear/issues?group=&amp;team=&amp;project=&amp;cycle=&amp;state=&amp;assignee=&amp;priority=&amp;label=&amp;after=&amp;limit=' (Linear hub spec §3; query): one page of the Work page's Linear list.
/// Every field is the raw query text, so a value outside its set is SND-API-013 from the service, not a binder failure.
/// </summary>
/// <param name="Group">mine (assigned to the viewer) or teams (the profile teams' issues not assigned to the viewer); null is mine.</param>
/// <param name="Team">A team key; null for every team.</param>
/// <param name="Project">A project's Linear id.</param>
/// <param name="Cycle">A cycle's Linear id.</param>
/// <param name="State">open (the default) or a state's Linear id.</param>
/// <param name="Assignee">A user's Linear id.</param>
/// <param name="Priority">0 to 4.</param>
/// <param name="Label">A label's Linear id.</param>
/// <param name="After">The previous page's next cursor.</param>
/// <param name="Limit">The page size, 1 to 200; null is 50.</param>
internal sealed record ListLinearIssuesRequest(
    string? Group = null,
    string? Team = null,
    string? Project = null,
    string? Cycle = null,
    string? State = null,
    string? Assignee = null,
    string? Priority = null,
    string? Label = null,
    string? After = null,
    string? Limit = null) : IQuery<ListLinearIssuesResponse>;

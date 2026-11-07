namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>GET /sindri/linear/issues/{key} (Linear hub spec §3): one issue with its description and newest comments. The row's fields sit at the top level of the answer.</summary>
/// <param name="Key">The issue key.</param>
/// <param name="Record">The context record id.</param>
/// <param name="Url">The issue in Linear.</param>
/// <param name="Title">The title.</param>
/// <param name="Team">The team.</param>
/// <param name="State">The workflow state.</param>
/// <param name="Priority">0 (none) to 4.</param>
/// <param name="Assignee">The assignee, or null.</param>
/// <param name="Project">The project, or null.</param>
/// <param name="Cycle">The cycle, or null.</param>
/// <param name="Labels">The labels.</param>
/// <param name="UpdatedAt">When Linear last changed it.</param>
/// <param name="Link">The Sindri item on it, or null.</param>
/// <param name="Pr">The linked pull request, or null.</param>
/// <param name="Viewer">Who "you" is; null until the viewer step has run.</param>
/// <param name="Description">The description (markdown).</param>
/// <param name="Comments">The newest 50 comments, newest first.</param>
/// <param name="Removed">True when Linear no longer serves it: this is the last stored copy and writes are off.</param>
internal sealed record LinearIssueOne(string Key, string Record, string Url, string Title, LinearTeamRef Team, LinearStateRef State, int Priority, LinearPerson? Assignee, LinearNamedRef? Project, LinearCycleRef? Cycle, IReadOnlyList<LinearLabel> Labels, DateTimeOffset UpdatedAt, LinearItemLink? Link, LinearPrRef? Pr, LinearPerson? Viewer, string Description, IReadOnlyList<LinearComment> Comments, bool Removed);

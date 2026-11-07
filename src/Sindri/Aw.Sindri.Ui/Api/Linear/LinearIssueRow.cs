namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>One issue in the Work page's Linear list (Linear hub spec §3).</summary>
/// <param name="Key">The issue key, such as ENG-12.</param>
/// <param name="Record">The context record id, linear:issue/&lt;uuid&gt;.</param>
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
internal sealed record LinearIssueRow(string Key, string Record, string Url, string Title, LinearTeamRef Team, LinearStateRef State, int Priority, LinearPerson? Assignee, LinearNamedRef? Project, LinearCycleRef? Cycle, IReadOnlyList<LinearLabel> Labels, DateTimeOffset UpdatedAt, LinearItemLink? Link, LinearPrRef? Pr);

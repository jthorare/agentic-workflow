namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>The list read's group and filters; a null filter is left out of the query.</summary>
/// <param name="Group">mine or teams.</param>
/// <param name="Team">A team key.</param>
/// <param name="Project">A project id.</param>
/// <param name="Cycle">A cycle id.</param>
/// <param name="State">open or a state id.</param>
/// <param name="Assignee">A user id.</param>
/// <param name="Priority">0 to 4.</param>
/// <param name="Label">A label id.</param>
/// <param name="After">The cursor of the page wanted.</param>
/// <param name="Limit">The page size.</param>
internal sealed record LinearIssueQuery(string Group, string? Team = null, string? Project = null, string? Cycle = null, string? State = null, string? Assignee = null, int? Priority = null, string? Label = null, string? After = null, int? Limit = null);

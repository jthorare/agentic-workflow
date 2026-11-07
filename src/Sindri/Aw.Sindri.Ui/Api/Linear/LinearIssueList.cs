namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>GET /sindri/linear/issues (Linear hub spec §3): one page of the Work page's Linear list. Not a view read: the answer carries no asOf. The session form's trust flags are not read.</summary>
/// <param name="Connected">False when no Linear source is set up (the page shows Connect Linear).</param>
/// <param name="Viewer">Who "you" is; null until the viewer step has run (the page shows Finding your issues).</param>
/// <param name="Feed">The feed's health.</param>
/// <param name="Group">mine or teams.</param>
/// <param name="Rows">The page's issues.</param>
/// <param name="Next">The cursor of the next page, or null on the last.</param>
/// <param name="Total">All matching issues, not only this page.</param>
internal sealed record LinearIssueList(bool Connected, LinearPerson? Viewer, LinearFeed Feed, string Group, IReadOnlyList<LinearIssueRow> Rows, string? Next, int Total);

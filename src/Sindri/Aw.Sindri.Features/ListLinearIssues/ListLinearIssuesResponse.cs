namespace Aw.Sindri.Features.ListLinearIssues;

/// <summary>One page of the Work page's Linear list (Linear hub spec §3). Not a view read: it carries no asOf.</summary>
/// <param name="Connected">False when no Linear source is set up (the page shows Connect Linear): the rows are empty.</param>
/// <param name="Viewer">Who "you" is; null until the viewer step has run (the mine group is then empty).</param>
/// <param name="Feed">The issue feed's health.</param>
/// <param name="Group">mine or teams.</param>
/// <param name="Rows">The page's issues.</param>
/// <param name="Next">The cursor of the next page, or null on the last.</param>
/// <param name="Total">The issues that match: for mine all of them; for teams the rows passed plus this page on the last page, and null while a next page exists (Sindri cannot know it).</param>
internal sealed record ListLinearIssuesResponse(bool Connected, LinearPersonView? Viewer, LinearFeedView Feed, string Group, IReadOnlyList<LinearIssueRowView> Rows, string? Next, int? Total);

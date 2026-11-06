namespace Aw.Context.Core.Linear;

/// <summary>One page of a list of stored Linear issues, newest first.</summary>
/// <param name="Issues">The issues.</param>
/// <param name="Next">The cursor for the next page, or null when this was the last.</param>
public sealed record LinearIssuePage(IReadOnlyList<LinearIssueRow> Issues, LinearIssueCursor? Next);

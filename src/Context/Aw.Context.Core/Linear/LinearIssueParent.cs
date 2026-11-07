namespace Aw.Context.Core.Linear;

/// <summary>An issue's parent as the detail read names it.</summary>
/// <param name="Key">The parent's human key, 'ENG-3'.</param>
/// <param name="Title">The parent's stored title.</param>
public sealed record LinearIssueParent(LinearIssueKey Key, string Title);

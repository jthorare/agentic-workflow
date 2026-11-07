namespace Aw.Sindri.Features.LinearIssues;

/// <summary>What one read of the stored issues narrows by; every field narrows, and none set lists every issue the caller may see.</summary>
/// <param name="Team">A team key, or null.</param>
/// <param name="Assignee">An assignee's Linear id, or null.</param>
/// <param name="StateTypes">State types to keep, or null for every type.</param>
/// <param name="Project">A project's Linear id, or null.</param>
/// <param name="Cycle">A cycle's Linear id, or null.</param>
/// <param name="Limit">The page size, 1 to 200.</param>
internal sealed record LinearIssueFactsFilter(string? Team, string? Assignee, IReadOnlyList<string>? StateTypes, string? Project, string? Cycle, int Limit);

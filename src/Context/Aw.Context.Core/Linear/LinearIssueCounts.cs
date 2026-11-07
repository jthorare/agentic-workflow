namespace Aw.Context.Core.Linear;

/// <summary>How many stored Linear issues a filter matches, by state type (Linear hub spec 2026-11-04 §7): what a list with the same filter would page through, without paging.</summary>
/// <param name="ByStateType">Per state type ('backlog', 'unstarted', 'started', 'completed', 'canceled', 'triage'), the issues of that type; a type with no issues is absent (read it as 0). An issue with no stored state type counts under 'unknown'.</param>
/// <param name="Total">The sum of every count, canceled included.</param>
public sealed record LinearIssueCounts(IReadOnlyDictionary<string, int> ByStateType, int Total);

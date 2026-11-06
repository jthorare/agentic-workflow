using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Linear;

/// <summary>Which stored Linear issues a list returns (Linear hub spec 2026-11-04 §4.3). Every field narrows; none set lists every issue the caller may see.</summary>
/// <param name="Team">A team key ('ENG'), or null for every team.</param>
/// <param name="Assignee">The assignee's Linear user id, or null.</param>
/// <param name="StateTypes">State types to keep ('started', 'unstarted', ...), or null for every type.</param>
/// <param name="Project">A project's Linear id, or null.</param>
/// <param name="Cycle">A cycle's Linear id, or null.</param>
/// <param name="Limit">How many issues one page holds, 1 to <see cref="MaxLimit"/>.</param>
public sealed record LinearIssueFilter(string? Team = null, ExternalId? Assignee = null, IReadOnlyList<string>? StateTypes = null, ExternalId? Project = null, ExternalId? Cycle = null, int Limit = LinearIssueFilter.DefaultLimit)
{
    /// <summary>The page size when none is asked for.</summary>
    public const int DefaultLimit = 50;

    /// <summary>The largest page.</summary>
    public const int MaxLimit = 200;
}

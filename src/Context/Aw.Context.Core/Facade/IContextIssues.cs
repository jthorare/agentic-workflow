using Aw.Context.Core.Engine;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Core.Facade;

/// <summary>The Linear hub's reads (Linear hub spec 2026-11-04 §4.3): stored issues with their fields, comments and lookups. They read the store only and apply the record's audience to the side tables, so an agent session sees only the issues of teams mapped to its repo. Nothing here calls Linear.</summary>
public interface IContextIssues
{
    /// <summary>Lists stored issues, newest first.</summary>
    /// <param name="filter">Which issues, and how many.</param>
    /// <param name="after">Where the page starts, or null for the first.</param>
    /// <param name="scope">The profile's settings and repos, and the caller: the audience rule applies as for any record read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One page, or SND-CONTEXT-001/004.</returns>
    public ValueTask<Result<LinearIssuePage>> IssueList(LinearIssueFilter filter, LinearIssueCursor? after, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Reads one stored issue: its record columns and its fields.</summary>
    /// <param name="key">The record key, 'linear:issue/&lt;uuid&gt;'.</param>
    /// <param name="scope">The profile's settings and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The issue, or null when it isn't stored, was removed, or the caller may not see it; SND-CONTEXT-001/004.</returns>
    public ValueTask<Result<LinearIssueRow?>> IssueGet(RecordKey key, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Reads one stored issue by its human key, with its parent and comments. Unlike <see cref="IssueGet(RecordKey, ContextScope, CancellationToken)"/> it reads past a removal: a removed issue comes back as its last stored copy, marked removed.</summary>
    /// <param name="key">The issue's key, 'ENG-12'.</param>
    /// <param name="scope">The profile's settings and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The issue whose stored identifier is the key, or null when none is stored or the caller may not see it; SND-CONTEXT-001/004. An issue moved to another team has its new identifier after the next sync; the old key reads null until then.</returns>
    public ValueTask<Result<LinearIssueDetail?>> IssueGet(LinearIssueKey key, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Counts stored issues by state type: what <see cref="IssueList"/> would page through with the same filter, audience and removed issues left out. The filter's limit is ignored.</summary>
    /// <param name="filter">Which issues.</param>
    /// <param name="scope">The profile's settings and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The counts (empty when the caller sees none); SND-CONTEXT-001/004.</returns>
    public ValueTask<Result<LinearIssueCounts>> IssueCounts(LinearIssueFilter filter, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Reads an issue's stored comments (the newest 50), newest first.</summary>
    /// <param name="key">The record key.</param>
    /// <param name="scope">The profile's settings and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The comments; none when the issue isn't visible; SND-CONTEXT-001/004.</returns>
    public ValueTask<Result<IReadOnlyList<LinearCommentRow>>> IssueComments(RecordKey key, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Reads a team's pickers: workflow states, cycles, members and labels. Owner-only: a session is refused.</summary>
    /// <param name="team">The team's key, 'ENG'.</param>
    /// <param name="scope">The profile's settings and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The lists, or null when none were stored for the team (or the team's audience isn't the caller's); SND-CONTEXT-020 for a session, 001/004.</returns>
    public ValueTask<Result<LinearTeamLookups?>> IssueLookups(string team, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Reads what changed for the viewer since a marker. Owner-only: a session is refused.</summary>
    /// <param name="marker">Only changes and comments after this instant count.</param>
    /// <param name="viewer">The viewer's Linear user id.</param>
    /// <param name="scope">The profile's settings and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The changes and mentions, oldest first; SND-CONTEXT-020 for a session, 001/004.</returns>
    public ValueTask<Result<LinearSince>> IssueSince(DateTimeOffset marker, ExternalId viewer, ContextScope scope, CancellationToken cancellationToken);
}

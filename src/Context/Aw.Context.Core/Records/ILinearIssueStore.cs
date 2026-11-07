using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Core.Records;

/// <summary>
/// Reads the stored Linear issues with their fields, comments and changes (Linear hub spec 2026-11-04 §4.3). Every read applies the caller's audience to the issue's record and hides tombstones and blocked records, and none calls Linear.
/// </summary>
public interface ILinearIssueStore
{
    /// <summary>Lists issues, newest first.</summary>
    /// <param name="filter">Which issues, and how many.</param>
    /// <param name="after">Where the page starts, or null for the first.</param>
    /// <param name="audience">What the caller may see.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One page.</returns>
    public ValueTask<Result<LinearIssuePage>> List(LinearIssueFilter filter, LinearIssueCursor? after, RecordAudience audience, CancellationToken cancellationToken);

    /// <summary>Reads one issue.</summary>
    /// <param name="key">The record key.</param>
    /// <param name="audience">What the caller may see.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The issue, or null when it isn't stored, is removed, or the caller may not see it.</returns>
    public ValueTask<Result<LinearIssueRow?>> Get(RecordKey key, RecordAudience audience, CancellationToken cancellationToken);

    /// <summary>Reads one issue by its human key, with its parent and comments, past a removal (the copy is marked removed).</summary>
    /// <param name="key">The issue's key, 'ENG-12'.</param>
    /// <param name="audience">What the caller may see.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The issue, or null when none has that identifier or the caller may not see it.</returns>
    public ValueTask<Result<LinearIssueDetail?>> Get(LinearIssueKey key, RecordAudience audience, CancellationToken cancellationToken);

    /// <summary>Counts the issues a list with this filter would page through, by state type.</summary>
    /// <param name="filter">Which issues; its limit is ignored.</param>
    /// <param name="audience">What the caller may see.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The counts.</returns>
    public ValueTask<Result<LinearIssueCounts>> Count(LinearIssueFilter filter, RecordAudience audience, CancellationToken cancellationToken);

    /// <summary>Reads an issue's stored comments, newest first.</summary>
    /// <param name="key">The record key.</param>
    /// <param name="audience">What the caller may see.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The comments; none when the issue isn't visible.</returns>
    public ValueTask<Result<IReadOnlyList<LinearCommentRow>>> Comments(RecordKey key, RecordAudience audience, CancellationToken cancellationToken);

    /// <summary>Reads what changed for a viewer after a marker: assignments to them, state moves a sync saw on their issues, and comments that mention them.</summary>
    /// <param name="marker">Only changes and comments after this instant count.</param>
    /// <param name="viewer">The viewer's Linear user id.</param>
    /// <param name="audience">What the caller may see.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The changes and mentions, oldest first.</returns>
    public ValueTask<Result<LinearSince>> Since(DateTimeOffset marker, ExternalId viewer, RecordAudience audience, CancellationToken cancellationToken);
}

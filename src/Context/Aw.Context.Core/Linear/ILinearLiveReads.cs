using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Core.Linear;

/// <summary>
/// One live read from Linear with the READ key, never the store (Linear hub spec 2026-11-04 D6, §5.5): the field-conflict check's input, the issue's id before a write, and the one re-read after a write got no answer.
/// Failures are the read client's (SND-CONTEXT-010 unreachable, 071 key refused, 072 unexpected reply); a missing record is null, not an error.
/// </summary>
public interface ILinearLiveReads
{
    /// <summary>Reads one issue.</summary>
    /// <param name="settings">The checked 'context.sources.linear' settings.</param>
    /// <param name="idOrKey">The issue's Linear id, or its key ('ENG-12').</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The issue, or null when Linear has none.</returns>
    public ValueTask<Result<LinearIssueView?>> Issue(LinearSettings settings, string idOrKey, CancellationToken cancellationToken);

    /// <summary>Reads one comment.</summary>
    /// <param name="settings">The checked settings.</param>
    /// <param name="commentId">The comment's Linear id (a client id the comment was created with is its id).</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The comment, or null when Linear has none.</returns>
    public ValueTask<Result<LinearCommentView?>> Comment(LinearSettings settings, ExternalId commentId, CancellationToken cancellationToken);

    /// <summary>Whether the issue has an attachment linking <paramref name="url"/>.</summary>
    /// <param name="settings">The checked settings.</param>
    /// <param name="issueId">The issue's Linear id.</param>
    /// <param name="url">The link.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True when it has one.</returns>
    public ValueTask<Result<bool>> HasAttachment(LinearSettings settings, ExternalId issueId, Uri url, CancellationToken cancellationToken);
}

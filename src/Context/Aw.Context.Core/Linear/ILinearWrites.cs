using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Core.Linear;

/// <summary>
/// Linear's mutations, sent with the write key (Linear hub spec 2026-11-04 §5.2, D2): nothing else holds that key, and nothing here reads. Every input is already checked (text limits, url shape) and every id already resolved;
/// the adapter sends one mutation per call and answers with the issue as the mutation's reply has it (the facade stores it and returns it, with no extra read). Failures: SND-LINEAR-002 when the settings name no write key, 004 the key refused, 005 no such issue or comment, 010 Linear unreachable before anything was
/// sent, 011 sent and no answer (timeout, reset, 5xx), 012 rate limited, 015 Linear refused it ('success: false', or errors next to data).
/// </summary>
public interface ILinearWrites
{
    /// <summary>Where the write key stands: the settings' pointer, resolved. Sends nothing to Linear.</summary>
    /// <param name="settings">The checked 'context.sources.linear' settings.</param>
    /// <param name="cancellationToken">Cancels the resolution.</param>
    /// <returns>The status.</returns>
    public ValueTask<LinearWriteKeyStatus> KeyStatus(LinearSettings settings, CancellationToken cancellationToken);

    /// <summary>issueCreate with <paramref name="clientId"/> as the issue's id.</summary>
    /// <param name="settings">The checked settings.</param>
    /// <param name="draft">The issue.</param>
    /// <param name="clientId">The id the issue is created with.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The issue as Linear answered after the write.</returns>
    public ValueTask<Result<LinearIssueView>> CreateIssue(LinearSettings settings, LinearIssueDraft draft, LinearClientId clientId, CancellationToken cancellationToken);

    /// <summary>issueUpdate with the fields of <paramref name="change"/> that are set, in one call.</summary>
    /// <param name="settings">The checked settings.</param>
    /// <param name="issueId">The issue's Linear id.</param>
    /// <param name="change">The fields to send; never empty.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The issue as Linear answered after the write.</returns>
    public ValueTask<Result<LinearIssueView>> UpdateIssue(LinearSettings settings, ExternalId issueId, LinearIssueChange change, CancellationToken cancellationToken);

    /// <summary>commentCreate with <paramref name="clientId"/> as the comment's id.</summary>
    /// <param name="settings">The checked settings.</param>
    /// <param name="issueId">The issue's Linear id.</param>
    /// <param name="body">The comment as markdown.</param>
    /// <param name="clientId">The id the comment is created with.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The comment's issue as Linear answered after the write.</returns>
    public ValueTask<Result<LinearIssueView>> CreateComment(LinearSettings settings, ExternalId issueId, string body, LinearClientId clientId, CancellationToken cancellationToken);

    /// <summary>commentUpdate: replaces a comment's text.</summary>
    /// <param name="settings">The checked settings.</param>
    /// <param name="commentId">The comment's Linear id.</param>
    /// <param name="body">The new text.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The comment's issue as Linear answered after the write.</returns>
    public ValueTask<Result<LinearIssueView>> UpdateComment(LinearSettings settings, ExternalId commentId, string body, CancellationToken cancellationToken);

    /// <summary>attachmentLinkURL: links <paramref name="url"/> to the issue (idempotent per issue and url).</summary>
    /// <param name="settings">The checked settings.</param>
    /// <param name="issueId">The issue's Linear id.</param>
    /// <param name="url">The link.</param>
    /// <param name="title">The attachment's title.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The issue as Linear answered after the write.</returns>
    public ValueTask<Result<LinearIssueView>> LinkUrl(LinearSettings settings, ExternalId issueId, Uri url, string title, CancellationToken cancellationToken);
}

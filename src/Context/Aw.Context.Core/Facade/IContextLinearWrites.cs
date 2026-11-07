using Aw.Context.Core.Engine;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Core.Facade;

/// <summary>
/// The Linear hub's writes (Linear hub spec 2026-11-04 §5.1-§5.5): one entry per kind of write, so Sindri never sequences a mutation and its write-through itself. Every method is the owner's: an agent session is refused
/// (SND-CONTEXT-020) before anything is read or sent. Inputs are ids the caller already resolved from <see cref="IContextIssues.IssueLookups"/>; Context checks nothing about teams or owners. Text limits (a title of 1 to 255 characters,
/// a description or comment of up to 65,536) are checked here too and refused as SND-LINEAR-015 before anything is sent. A refusal comes back as a Result error with its SND-LINEAR code (004 key refused, 005 not found, 010 unreachable,
/// 012 rate limited, 015 Linear said no); a write that was sent comes back as a <see cref="LinearWriteOutcome"/>, and an unknown outcome is a value, not an error.
/// </summary>
public interface IContextLinearWrites
{
    /// <summary>Reads one issue live from Linear with the read key, not from the store: the input of the field-conflict check (spec D6).</summary>
    /// <param name="key">The issue's key.</param>
    /// <param name="scope">The profile's settings and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The issue as Linear has it now; SND-LINEAR-005 when there is none, 010 when Linear is unreachable; SND-CONTEXT-020 for a session.</returns>
    public ValueTask<Result<LinearIssueView>> Live(LinearIssueKey key, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Where the write key stands. Reads the profile and the secret store only; nothing is sent to Linear.</summary>
    /// <param name="scope">The profile's settings and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The status; SND-CONTEXT-020 for a session.</returns>
    public ValueTask<Result<LinearWriteKeyStatus>> WriteKeyStatus(ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Creates an issue. When Linear gives no answer, the one re-read looks for the issue by <paramref name="clientId"/>.</summary>
    /// <param name="draft">The issue.</param>
    /// <param name="clientId">The id the issue is created with; a retry sends the same one.</param>
    /// <param name="scope">The profile's settings and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>How it ended; SND-LINEAR-015 for text over a limit or a validation refusal, 004, 010, 012; SND-CONTEXT-020 for a session.</returns>
    public ValueTask<Result<LinearWriteOutcome>> Create(LinearIssueDraft draft, LinearClientId clientId, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Changes the fields that are set. Takes no client id: when Linear gives no answer, the one re-read compares the sent fields' values, and all of them at the new value counts as applied.</summary>
    /// <param name="key">The issue's key.</param>
    /// <param name="change">The fields to send.</param>
    /// <param name="scope">The profile's settings and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>How it ended; SND-LINEAR-005, 015, 004, 010, 012; SND-CONTEXT-020 for a session.</returns>
    public ValueTask<Result<LinearWriteOutcome>> Change(LinearIssueKey key, LinearIssueChange change, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Posts a comment. When Linear gives no answer, the one re-read looks for the comment by <paramref name="clientId"/>.</summary>
    /// <param name="key">The issue's key.</param>
    /// <param name="body">The comment as markdown, 1 to 65,536 characters.</param>
    /// <param name="clientId">The id the comment is created with; a retry sends the same one.</param>
    /// <param name="scope">The profile's settings and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>How it ended; SND-LINEAR-005, 015, 004, 010, 012; SND-CONTEXT-020 for a session.</returns>
    public ValueTask<Result<LinearWriteOutcome>> Comment(LinearIssueKey key, string body, LinearClientId clientId, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Replaces a comment's text. Takes no client id: when Linear gives no answer, the one re-read compares the comment's text.</summary>
    /// <param name="key">The issue's key.</param>
    /// <param name="commentId">The comment's Linear id.</param>
    /// <param name="body">The new text, 1 to 65,536 characters.</param>
    /// <param name="scope">The profile's settings and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>How it ended; SND-LINEAR-005 (also for a deleted comment), 015, 004, 010, 012; SND-CONTEXT-020 for a session.</returns>
    public ValueTask<Result<LinearWriteOutcome>> EditComment(LinearIssueKey key, ExternalId commentId, string body, ContextScope scope, CancellationToken cancellationToken);

    /// <summary>Links a url (a PR) to an issue as an attachment. Takes no client id: Linear's attachment-by-url is idempotent per issue and url, so when Linear gives no answer, the one re-read looks for the url on the issue.</summary>
    /// <param name="key">The issue's key.</param>
    /// <param name="url">The link.</param>
    /// <param name="title">The attachment's title.</param>
    /// <param name="scope">The profile's settings and repos, and the caller.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>How it ended; SND-LINEAR-005, 015, 004, 010, 012; SND-CONTEXT-020 for a session.</returns>
    public ValueTask<Result<LinearWriteOutcome>> Attach(LinearIssueKey key, Uri url, string title, ContextScope scope, CancellationToken cancellationToken);
}

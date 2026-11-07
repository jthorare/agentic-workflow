using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Features.WriteLinear;

/// <summary>
/// The Linear hub's writes (spec 2026-11-04 §5.1-§5.5). Each write goes in this order: the caller (a session is refused, SND-CONTEXT-020, before anything else), the profile's Linear settings (SND-CONTEXT-002 when none), the local checks (SND-LINEAR-015,
/// before anything is sent), one live read of the issue (SND-LINEAR-005 when Linear has none, 010 when it can't be reached), the mutation, and on its answer the write-through into the store (<see cref="LinearWriteApplied"/>; the store failing after
/// the mutation landed is <see cref="LinearWriteAppliedNotStored"/>, never an error). When the mutation was sent and got no answer (SND-LINEAR-011) the issue, comment or attachment is read once more: found counts as applied, not found or unreadable is
/// <see cref="LinearWriteUnknown"/>. EditComment also reads the comment live before sending and refuses one that isn't on the issue (SND-LINEAR-005). Which teams a write may touch (SND-LINEAR-003: Create's team id, an empty
/// teams list) and a write key that equals the read key (SND-LINEAR-001) are refused before this facade is called, by the caller's LinearWrite.Begin (api) and the profile check.
/// </summary>
/// <param name="writes">The mutations, sent with the write key.</param>
/// <param name="reads">The live reads, sent with the read key.</param>
/// <param name="through">The write-through into the store.</param>
internal sealed class ContextLinearWrites(ILinearWrites writes, ILinearLiveReads reads, ILinearWriteThrough through) : IContextLinearWrites
{
    /// <inheritdoc />
    public async ValueTask<Result<LinearIssueView>> Live(LinearIssueKey key, ContextScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        var settings = Settings(scope);
        return settings.IsError ? settings.Error : await Resolve(settings.Value, key, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<Result<LinearWriteKeyStatus>> WriteKeyStatus(ContextScope scope, CancellationToken cancellationToken)
    {
        var settings = Settings(scope);
        return settings.IsError ? settings.Error : await writes.KeyStatus(settings.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<Result<LinearWriteOutcome>> Create(LinearIssueDraft draft, LinearClientId clientId, ContextScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(clientId);
        var settings = Settings(scope);
        if ((settings.IsError ? settings.Error : LinearWriteLimits.Title(draft.Title) ?? LinearWriteLimits.Text("description", draft.Description, required: false)) is { } refused)
        {
            return refused;
        }

        var written = (draft.AssigneeId is null ? LinearWrittenFields.None : LinearWrittenFields.Assignee) | (draft.StateId is null ? LinearWrittenFields.None : LinearWrittenFields.State);
        var sent = await writes.CreateIssue(settings.Value, draft, clientId, cancellationToken).ConfigureAwait(false);
        return await Finish(scope, sent, written, () => ReadIssue(settings.Value, clientId.Value, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<Result<LinearWriteOutcome>> Change(LinearIssueKey key, LinearIssueChange change, ContextScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(change);
        var prepared = await Prepare(scope, LinearWriteLimits.Change(change), key, cancellationToken).ConfigureAwait(false);
        if (prepared.IsError)
        {
            return prepared.Error;
        }

        var (settings, issue) = prepared.Value;
        var written = (change.AssigneeId.IsSet ? LinearWrittenFields.Assignee : LinearWrittenFields.None) | (change.StateId.IsSet ? LinearWrittenFields.State : LinearWrittenFields.None);
        var sent = await writes.UpdateIssue(settings, issue.Id, change, cancellationToken).ConfigureAwait(false);
        return await Finish(scope, sent, written, async () => (await ReadIssue(settings, issue.Id.Value, cancellationToken).ConfigureAwait(false)) is { } now && LinearChangeApplied.In(change, now) ? now : null, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<Result<LinearWriteOutcome>> Comment(LinearIssueKey key, string body, LinearClientId clientId, ContextScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(clientId);
        var prepared = await Prepare(scope, LinearWriteLimits.Text("comment", body, required: true), key, cancellationToken).ConfigureAwait(false);
        if (prepared.IsError)
        {
            return prepared.Error;
        }

        var (settings, issue) = prepared.Value;
        var sent = await writes.CreateComment(settings, issue.Id, body, clientId, cancellationToken).ConfigureAwait(false);
        return await Finish(scope, sent, LinearWrittenFields.None, async () =>
        {
            var comment = await reads.Comment(settings, ExternalId.From(clientId.Value), cancellationToken).ConfigureAwait(false);
            return comment.IsSuccess && comment.Value is { } found && found.IssueId == issue.Id ? issue : null;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<Result<LinearWriteOutcome>> EditComment(LinearIssueKey key, ExternalId commentId, string body, ContextScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        var prepared = await Prepare(scope, LinearWriteLimits.Text("comment", body, required: true), key, cancellationToken).ConfigureAwait(false);
        if (prepared.IsError)
        {
            return prepared.Error;
        }

        var (settings, issue) = prepared.Value;
        var owned = await OwnComment(settings, issue, commentId, cancellationToken).ConfigureAwait(false);
        if (owned is not null)
        {
            return owned;
        }

        var sent = await writes.UpdateComment(settings, commentId, body, cancellationToken).ConfigureAwait(false);
        return await Finish(scope, sent, LinearWrittenFields.None, async () =>
        {
            var comment = await reads.Comment(settings, commentId, cancellationToken).ConfigureAwait(false);
            return comment.IsSuccess && comment.Value is { } found && string.Equals(found.Body, body, StringComparison.Ordinal) ? issue : null;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<Result<LinearWriteOutcome>> Attach(LinearIssueKey key, Uri url, string title, ContextScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(url);
        var prepared = await Prepare(scope, LinearWriteLimits.Attachment(url, title), key, cancellationToken).ConfigureAwait(false);
        if (prepared.IsError)
        {
            return prepared.Error;
        }

        var (settings, issue) = prepared.Value;
        var sent = await writes.LinkUrl(settings, issue.Id, url, title, cancellationToken).ConfigureAwait(false);
        return await Finish(scope, sent, LinearWrittenFields.None, async () =>
        {
            var has = await reads.HasAttachment(settings, issue.Id, url, cancellationToken).ConfigureAwait(false);
            return has.IsSuccess && has.Value ? issue : null;
        }, cancellationToken).ConfigureAwait(false);
    }

    // why: the caller, then the settings, then the local checks, then the live read: a refusal further down never costs a request
    private async ValueTask<Result<(LinearSettings Settings, LinearIssueView Issue)>> Prepare(ContextScope scope, Error? local, LinearIssueKey key, CancellationToken cancellationToken)
    {
        var settings = Settings(scope);
        if (settings.IsError)
        {
            return settings.Error;
        }

        if (local is not null)
        {
            return local;
        }

        var issue = await Resolve(settings.Value, key, cancellationToken).ConfigureAwait(false);
        return issue.IsError ? issue.Error : (settings.Value, issue.Value);
    }

    private static Result<LinearSettings> Settings(ContextScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (scope.Caller.Kind == ContextCallerKind.Session)
        {
            return ContextErrors.SessionsDisabled.With("Linear writes are the owner's, not an agent session's");
        }

        var settings = SettingsReader.Read(scope.Settings);
        if (settings.IsError)
        {
            return settings.Error;
        }

        return settings.Value.Linear is { } linear
            ? linear
            : ContextErrors.SourceNotConfigured.With("context.sources.linear: not configured, so there is no Linear to write to");
    }

    private async ValueTask<Result<LinearIssueView>> Resolve(LinearSettings settings, LinearIssueKey key, CancellationToken cancellationToken)
    {
        var read = await reads.Issue(settings, key.ToString(), cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error.Code == ContextErrors.SourceUnavailable.Code ? LinearHubErrors.Unreachable.With(read.Error.Message) : read.Error;
        }

        return read.Value ?? (Result<LinearIssueView>)LinearHubErrors.IssueNotFound.With($"No issue {key} in your teams (deleted, moved, or not synced yet: `sindri linear show` after the next tick).");
    }

    // why: the team check passed on the issue, so the comment has to be on that issue: an id from an issue outside the teams is never sent
    private async ValueTask<Error?> OwnComment(LinearSettings settings, LinearIssueView issue, ExternalId commentId, CancellationToken cancellationToken)
    {
        var read = await reads.Comment(settings, commentId, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error.Code == ContextErrors.SourceUnavailable.Code ? LinearHubErrors.Unreachable.With(read.Error.Message) : read.Error;
        }

        return read.Value is { } found && found.IssueId == issue.Id
            ? null
            : LinearHubErrors.IssueNotFound.With($"No comment {commentId.Value} on {issue.Key}, so nothing was sent.");
    }

    // why: a re-read that fails is no answer either, so the caller hears Unknown, never an error for a write that may have landed
    private async ValueTask<LinearIssueView?> ReadIssue(LinearSettings settings, string idOrKey, CancellationToken cancellationToken)
    {
        var read = await reads.Issue(settings, idOrKey, cancellationToken).ConfigureAwait(false);
        return read.IsSuccess ? read.Value : null;
    }

    private async ValueTask<Result<LinearWriteOutcome>> Finish(ContextScope scope, Result<LinearIssueView> sent, LinearWrittenFields written, Func<ValueTask<LinearIssueView?>> reread, CancellationToken cancellationToken)
    {
        if (sent.IsSuccess)
        {
            return await Store(scope, sent.Value, written, cancellationToken).ConfigureAwait(false);
        }

        if (sent.Error.Code != LinearHubErrors.NoAnswer.Code)
        {
            return sent.Error;
        }

        return await reread().ConfigureAwait(false) is { } found
            ? await Store(scope, found, written, cancellationToken).ConfigureAwait(false)
            : new LinearWriteUnknown();
    }

    private async ValueTask<Result<LinearWriteOutcome>> Store(ContextScope scope, LinearIssueView issue, LinearWrittenFields written, CancellationToken cancellationToken) =>
        await through.Store(scope, issue.Id, written, cancellationToken).ConfigureAwait(false) ? new LinearWriteApplied(issue) : new LinearWriteAppliedNotStored(issue);
}

using Aw.Kernel;
using Aw.Sindri.Ui.Api.Linear;
using Aw.Sindri.Ui.ViewModels.Run;

namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>
/// The sheet's comment box (<see cref="LinearIssueSheetViewModel"/>, continued; Linear hub spec §3, "Issue sheet, Post comment"): separate from the form, with its own Post
/// (⌘Enter) and its own client id, made at the box's first character and sent again by Retry. Typed text counts as unsaved for the discard question.
/// </summary>
internal sealed partial class LinearIssueSheetViewModel
{
    private const int CommentCap = 65_536;

    private static readonly TimeSpan _sendingAfter = TimeSpan.FromMilliseconds(300);

    private string? _clientId;
    /// <summary>The comment being typed.</summary>
    public string CommentText
    {
        get;
        set
        {
            if (string.Equals(field, value, StringComparison.Ordinal))
            {
                return;
            }

            var wasEmpty = field.Length == 0;
            Set(ref field, value);
            if (wasEmpty && value.Length > 0 && _clientId is null)
            {
                _clientId = Guid.NewGuid().ToString();
            }

            if (!IsCommentLocked)
            {
                CommentError = string.Empty;
            }

            NotifyComment();
            SettleQuestion();
        }
    } = string.Empty;

    /// <summary>True when the box takes typing: the owner may write and no comment is out or unknown.</summary>
    public bool CanComment => CanEdit && !IsCommentLocked;

    /// <summary>True when Post works: something other than spaces is typed, within the cap, and the box takes typing.</summary>
    public bool CanPost => CanComment && HasCommentText && CommentText.Length <= CommentCap;

    /// <summary>The Post button's text: "Sending…" once a post has been out 300 ms.</summary>
    public string PostText { get; private set => Set(ref field, value); } = Labels.LinearPost;

    /// <summary>True while a comment is out.</summary>
    public bool IsPosting { get; private set => Set(ref field, value); }

    /// <summary>The box's polite line: Posted, Checking or Retry?; empty when none.</summary>
    public string CommentStatus
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasCommentStatus));
        }
    } = string.Empty;

    /// <summary>True when the box's polite line has words.</summary>
    public bool HasCommentStatus => CommentStatus.Length > 0;

    /// <summary>The banner for a refused comment; empty when none.</summary>
    public string CommentError
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasCommentError));
        }
    } = string.Empty;

    /// <summary>True when the comment banner shows.</summary>
    public bool HasCommentError => CommentError.Length > 0;

    /// <summary>The line under an over-long comment; empty when it fits.</summary>
    public string CommentNote => CommentText.Length > CommentCap ? Labels.LinearCommentCap : string.Empty;

    /// <summary>True when the note under the box shows.</summary>
    public bool HasCommentNote => CommentNote.Length > 0;

    /// <summary>True after a comment with no answer: Retry sends the same client id and text.</summary>
    public bool CanRetryComment => CommentUnknown;

    /// <summary>The box's accessible name.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string CommentName => Labels.LinearCommentName;

    /// <summary>The box's placeholder.</summary>
    public string CommentPlaceholder => Labels.LinearCommentPlaceholder;
#pragma warning restore CA1822, MA0041

    /// <summary>True when anything would be lost by leaving: a changed field or typed comment text.</summary>
    public bool HasUnsaved => HasChanges || HasCommentText;

    private bool CommentUnknown { get; set; }

    private bool IsCommentLocked => IsPosting || CommentUnknown;

    private bool HasCommentText => !string.IsNullOrWhiteSpace(CommentText);

    /// <summary>Posts the comment (the Post button, ⌘Enter in the box). Nothing happens when Post is off.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that ends when the answer has been drawn.</returns>
    internal Task Post(CancellationToken cancellationToken) => CanPost && _clientId is { } id ? SendComment(id, CommentText, cancellationToken) : Task.CompletedTask;

    /// <summary>After a comment with no answer: sends it again with the same client id.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that ends when the answer has been drawn.</returns>
    internal Task RetryComment(CancellationToken cancellationToken) => CommentUnknown && CanEdit && !IsPosting && _clientId is { } id ? SendComment(id, CommentText, cancellationToken) : Task.CompletedTask;

    private async Task SendComment(string clientId, string text, CancellationToken cancellationToken)
    {
        if (_url is not { } url)
        {
            return;
        }

        var epoch = _epoch;
        CommentUnknown = false;
        (CommentStatus, CommentError) = (string.Empty, string.Empty);
        IsPosting = true;
        NotifyComment();
        await using var sending = _clock.CreateTimer(_ => _ui.PostOrInline(() => CommentSending(epoch)), state: null, _sendingAfter, Timeout.InfiniteTimeSpan).ConfigureAwait(true);
        await using var checking = _clock.CreateTimer(_ => _ui.PostOrInline(() => CommentChecking(epoch)), state: null, _checkAfter, Timeout.InfiniteTimeSpan).ConfigureAwait(true);
        var answer = await Api.LinearComment(url, Key, clientId, text, cancellationToken).ConfigureAwait(true);
        if (epoch != _epoch)
        {
            return;   // another issue took the sheet over meanwhile
        }

        (IsPosting, PostText) = (false, Labels.LinearPost);
        if (answer.IsSuccess)
        {
            CommentAnswered(answer.Value);
        }
        else
        {
            CommentRefused(answer.Error);
        }

        NotifyComment();
        SettleQuestion();
    }

    private void CommentSending(int epoch)
    {
        if (IsPosting && epoch == _epoch)
        {
            PostText = Labels.LinearSending;
        }
    }

    private void CommentChecking(int epoch)
    {
        if (IsPosting && epoch == _epoch)
        {
            CommentStatus = Labels.LinearChecking(Key);
        }
    }

    private void CommentAnswered(LinearWriteOutcome outcome)
    {
        switch (outcome)
        {
            case LinearCommentPosted posted:
                CommentPosted(posted);
                break;
            case LinearWriteUnknown:
                CommentUnknown = true;
                CommentStatus = Labels.LinearCommentUnknown;
                break;
            case LinearWriteRateLimited limited:
                CommentError = Labels.LinearRateLimited(StatusText.Clock(limited.RetryAfter));
                break;
            default:
                break;
        }
    }

    private void CommentPosted(LinearCommentPosted posted)
    {
        var c = posted.Comment;
        var now = _clock.GetUtcNow();
        Comments = [new LinearCommentViewModel(c.Author.Name, Labels.AgoWords(now - c.CreatedAt), LinearText.Lines(c.Body, Open), c.Sindri), .. Comments];
        _clientId = null;
        CommentText = string.Empty;
        CommentStatus = posted.Slow ? Labels.LinearPostedSlow : Labels.LinearPosted;
        FocusRequested?.Invoke(this, new ValueEventArgs<LinearIssueFocus>(LinearIssueFocus.CommentBox));
    }

    private void CommentRefused(Error error)
    {
        if (string.Equals(error.Code, "SND-AUTH-002", StringComparison.Ordinal))
        {
            CanEdit = false;   // a session may not write: the box reads as off, with no banner
            return;
        }

        CommentError = Labels.LinearWriteError(error, _issue?.Team.Key ?? string.Empty, Key);
    }

    // The comment's own state goes: another issue took the sheet, or Discard dropped the typed text.
    private void ClearComment()
    {
        (_clientId, CommentUnknown) = (null, false);
        (IsPosting, PostText) = (false, Labels.LinearPost);
        (CommentStatus, CommentError) = (string.Empty, string.Empty);
        CommentText = string.Empty;
        NotifyComment();
    }

    private void NotifyComment()
    {
        OnChanged(nameof(CanComment));
        OnChanged(nameof(CanPost));
        OnChanged(nameof(CanRetryComment));
        OnChanged(nameof(CommentNote));
        OnChanged(nameof(HasCommentNote));
        OnChanged(nameof(HasUnsaved));
        OnChanged(nameof(DiscardBody));
    }
}

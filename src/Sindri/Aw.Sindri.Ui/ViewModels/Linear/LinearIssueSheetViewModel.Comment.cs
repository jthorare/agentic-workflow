namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>
/// The sheet's comment box (<see cref="LinearIssueSheetViewModel"/>, continued; Linear hub spec §3, "Issue sheet, Post comment"): separate from the form, with its own Post
/// (⌘Enter) and its own client id, made at the box's first character and sent again by Retry. Typed text counts as unsaved for the discard question.
/// </summary>
internal sealed partial class LinearIssueSheetViewModel
{
    /// <summary>The comment being typed.</summary>
    public string CommentText { get; set; } = string.Empty;

    /// <summary>True when the box takes typing: the owner may write and no comment is out or unknown.</summary>
    public bool CanComment { get; private set; }

    /// <summary>True when Post works: something other than spaces is typed, within the cap, and the box takes typing.</summary>
    public bool CanPost { get; private set; }

    /// <summary>The Post button's text: "Sending…" once a post has been out 300 ms.</summary>
    public string PostText { get; private set; } = string.Empty;

    /// <summary>True while a comment is out.</summary>
    public bool IsPosting { get; private set; }

    /// <summary>The box's polite line: Posted, Checking or Retry?; empty when none.</summary>
    public string CommentStatus { get; private set; } = string.Empty;

    /// <summary>The banner for a refused comment; empty when none.</summary>
    public string CommentError { get; private set; } = string.Empty;

    /// <summary>The line under an over-long comment; empty when it fits.</summary>
    public string CommentNote { get; private set; } = string.Empty;

    /// <summary>True after a comment with no answer: Retry sends the same client id and text.</summary>
    public bool CanRetryComment { get; private set; }

    /// <summary>True when anything would be lost by leaving: a changed field or typed comment text.</summary>
    public bool HasUnsaved => HasChanges;

    /// <summary>Posts the comment (the Post button, ⌘Enter in the box). Nothing happens when Post is off.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that ends when the answer has been drawn.</returns>
    internal Task Post(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>After a comment with no answer: sends it again with the same client id.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that ends when the answer has been drawn.</returns>
    internal Task RetryComment(CancellationToken cancellationToken) => throw new NotSupportedException();
}

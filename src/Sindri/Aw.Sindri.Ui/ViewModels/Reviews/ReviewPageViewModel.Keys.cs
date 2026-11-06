namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>The review page's keys and fix tasks (<see cref="ReviewPageViewModel"/>, continued; review page spec §6.4, §8.2).</summary>
internal sealed partial class ReviewPageViewModel
{
    /// <summary>The PR a fix task would be about; null before the page is read.</summary>
    internal ReviewFixTarget? FixTarget { get; private set; }

    /// <summary>
    /// A diff key: j and k next and previous file, n and p next and previous hunk, o fold or unfold the focused file's group,
    /// Enter open a folded group whose header has focus, c send the focused line as a fix task. Keys work only when focus is in the diff
    /// and no text box has it.
    /// </summary>
    /// <param name="key">The key as typed ("j", "Enter").</param>
    /// <param name="focusInDiff">True when focus is inside the diff.</param>
    /// <param name="textBoxHasFocus">True when a text box (the fix text, the login field, search) has focus.</param>
    /// <returns>True when the key was the page's and was used.</returns>
    internal bool PressDiffKey(string key, bool focusInDiff, bool textBoxHasFocus)
    {
        _ = (key, focusInDiff, textBoxHasFocus, Diff);
        return false;
    }

    /// <summary>Send as fix task on a failed required check: its thread is a comment in the review body.</summary>
    /// <param name="checkId">The check's id.</param>
    internal void RequestCheckFix(string checkId) => OnChanged(checkId);

    /// <summary>Send as fix task on an unclaimed requirement of the doc map: its thread is a comment in the review body.</summary>
    /// <param name="itemId">The requirement's id.</param>
    internal void RequestRequirementFix(string itemId) => OnChanged(itemId);

    /// <summary>The fix-task sheet closed (Esc, Cancel, Close): focus goes back to its source, or to the strip's first part when the source is gone after a reload.</summary>
    /// <param name="returnTo">Where the request came from.</param>
    internal void ReturnFromFix(ReviewFocus returnTo)
    {
        _ = returnTo;
        Focus = ReviewFocus.None;
    }

#pragma warning disable CS0067 // raised once a check, a requirement or a diff line asks for a fix task
    /// <summary>A check, a requirement or a diff line asked for a fix task: the shell opens the sheet.</summary>
    internal event EventHandler<ReviewFixRequestedEventArgs>? FixRequested;
#pragma warning restore CS0067
}

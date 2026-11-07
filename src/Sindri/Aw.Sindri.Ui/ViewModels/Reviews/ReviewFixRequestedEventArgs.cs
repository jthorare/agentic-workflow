namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>The page asked for a fix task: the finding, the PR, and where focus returns when the sheet closes.</summary>
/// <param name="source">The finding.</param>
/// <param name="target">The PR.</param>
/// <param name="returnTo">The diff line or panel row the request came from.</param>
internal sealed class ReviewFixRequestedEventArgs(ReviewFixSource source, ReviewFixTarget target, ReviewFocus returnTo) : EventArgs
{
    /// <summary>The finding.</summary>
    public ReviewFixSource Source { get; } = source;

    /// <summary>The PR.</summary>
    public ReviewFixTarget Target { get; } = target;

    /// <summary>Where focus returns on Esc, Cancel or Close.</summary>
    public ReviewFocus ReturnTo { get; } = returnTo;
}

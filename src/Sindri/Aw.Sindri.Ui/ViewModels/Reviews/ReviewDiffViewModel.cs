namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>The diff, the page's right pane (review page spec §6.2, §6.3): its state and words, the file count and the folded or open groups.</summary>
internal sealed class ReviewDiffViewModel : ObservableObject
{
    /// <summary>Loading, ready, empty or error.</summary>
    public ReviewPanelState State { get; } = ReviewPanelState.Loading;

    /// <summary>The words of a loading, empty or error diff: "Reading files…", "This PR changes no files", "Couldn't read the diff: …".</summary>
    public string Text { get; } = string.Empty;

    /// <summary>"4 files", or "3,000 files" at GitHub's cap.</summary>
    public string CountText { get; } = string.Empty;

    /// <summary>"GitHub lists the first 3,000 files; open the rest on GitHub" at the cap; empty otherwise.</summary>
    public string CappedText { get; } = string.Empty;

    /// <summary>The groups, in order.</summary>
    public IReadOnlyList<ReviewDiffGroupViewModel> Groups { get; } = [];

    /// <summary>The file the diff keys act on (j and k move it); null before the first key and after its group folds.</summary>
    public ReviewDiffFileViewModel? FocusedFile { get; }

    /// <summary>The hunk the diff keys act on (n and p move it); null when the focused file has none.</summary>
    public ReviewDiffHunkViewModel? FocusedHunk { get; }

    /// <summary>The line `c` sends as a fix task; null when no line has focus.</summary>
    public ReviewDiffLineViewModel? FocusedLine { get; }

    /// <summary>The files in the order j and k walk them: the files of the groups that are open.</summary>
    public IReadOnlyList<ReviewDiffFileViewModel> VisibleFiles { get; } = [];

    /// <summary>A line was clicked or focused: it becomes the focused line, with its hunk and file.</summary>
    /// <param name="line">The line.</param>
    internal void FocusLine(ReviewDiffLineViewModel line) => OnChanged(line.AutomationName);
}

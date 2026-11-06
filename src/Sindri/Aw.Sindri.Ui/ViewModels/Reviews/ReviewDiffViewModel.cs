namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>The diff, the page's right pane (review page spec §6.2, §6.3): its state and words, the file count and the folded or open groups.</summary>
internal sealed class ReviewDiffViewModel : ObservableObject
{
    /// <summary>Loading, ready, empty or error.</summary>
    public ReviewPanelState State { get; private set => Set(ref field, value); } = ReviewPanelState.Loading;

    /// <summary>The words of a loading, empty or error diff: "Reading files…", "This PR changes no files", "Couldn't read the diff: …".</summary>
    public string Text { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"4 files", or "3,000 files" at GitHub's cap.</summary>
    public string CountText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"GitHub lists the first 3,000 files; open the rest on GitHub" at the cap; empty otherwise.</summary>
    public string CappedText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The groups, in order.</summary>
    public IReadOnlyList<ReviewDiffGroupViewModel> Groups { get; private set => Set(ref field, value); } = [];

    /// <summary>Back to the placeholder while the page is read.</summary>
    internal void ShowLoading() => (State, Text, CountText, CappedText, Groups) = (ReviewPanelState.Loading, Labels.ReviewDiffReading, string.Empty, string.Empty, []);

    /// <summary>Shows the served diff.</summary>
    /// <param name="diff">The diff.</param>
    internal void Show(Api.Reviews.ReviewDiffView diff)
    {
        var groups = diff.Groups.Select(g => new ReviewDiffGroupViewModel(g)).ToList();
        var state = diff.State switch
        {
            "empty" => ReviewPanelState.Empty,
            "error" => ReviewPanelState.Error,
            _ => ReviewPanelState.Ready,
        };
        (State, Text, Groups) = (state, diff.Text ?? string.Empty, groups);
        (CountText, CappedText) = (state == ReviewPanelState.Ready ? Labels.ReviewFiles(diff.FileCount) : string.Empty, diff.CappedText ?? string.Empty);
    }

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

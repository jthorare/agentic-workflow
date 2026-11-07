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
    internal void ShowLoading()
    {
        (State, Text, CountText, CappedText, Groups) = (ReviewPanelState.Loading, Labels.ReviewDiffReading, string.Empty, string.Empty, []);
        (FocusedFile, FocusedHunk, FocusedLine) = (null, null, null);
    }

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
        foreach (var group in groups)
        {
            group.PropertyChanged += OnGroupChanged;
        }

        (State, Text, Groups) = (state, diff.Text ?? string.Empty, groups);
        (FocusedFile, FocusedHunk, FocusedLine) = (null, null, null);
        (CountText, CappedText) = (state == ReviewPanelState.Ready ? Labels.ReviewFiles(diff.FileCount) : string.Empty, diff.CappedText ?? string.Empty);
    }

    /// <summary>The file the diff keys act on (j and k move it); null before the first key and after its group folds.</summary>
    public ReviewDiffFileViewModel? FocusedFile { get; private set => Set(ref field, value); }

    /// <summary>The hunk the diff keys act on (n and p move it); null when the focused file has none.</summary>
    public ReviewDiffHunkViewModel? FocusedHunk { get; private set => Set(ref field, value); }

    /// <summary>The line `c` sends as a fix task; null when no line has focus.</summary>
    public ReviewDiffLineViewModel? FocusedLine { get; private set => Set(ref field, value); }

    /// <summary>The files in the order j and k walk them: the files of the groups that are open.</summary>
    public IReadOnlyList<ReviewDiffFileViewModel> VisibleFiles => [.. Groups.Where(g => !g.IsFolded).SelectMany(g => g.Files)];

    /// <summary>j and k: the next or previous file of the open groups, stopping at both ends; its first hunk and line get focus.</summary>
    /// <param name="delta">1 or -1.</param>
    internal void MoveFile(int delta)
    {
        var files = VisibleFiles;
        if (files.Count == 0)
        {
            return;
        }

        var at = FocusedFile is null ? -1 : files.ToList().IndexOf(FocusedFile);
        var file = files[Math.Clamp(at < 0 ? 0 : at + delta, 0, files.Count - 1)];
        Focus(file, file.Hunks is [var first, ..] ? first : null);
    }

    /// <summary>n and p: the next or previous hunk across the open files, stopping at both ends; its first line gets focus.</summary>
    /// <param name="delta">1 or -1.</param>
    internal void MoveHunk(int delta)
    {
        var hunks = VisibleFiles.SelectMany(f => f.Hunks.Select(h => (File: f, Hunk: h))).ToList();
        if (hunks.Count == 0)
        {
            return;
        }

        var at = hunks.FindIndex(h => ReferenceEquals(h.Hunk, FocusedHunk));
        var (file, hunk) = hunks[Math.Clamp(at < 0 ? 0 : at + delta, 0, hunks.Count - 1)];
        Focus(file, hunk);
    }

    /// <summary>A line was clicked or focused: it becomes the focused line, with its hunk and file.</summary>
    /// <param name="line">The line.</param>
    internal void FocusLine(ReviewDiffLineViewModel line)
    {
        foreach (var file in VisibleFiles)
        {
            if (file.Hunks.FirstOrDefault(h => h.Lines.Contains(line)) is { } hunk)
            {
                (FocusedFile, FocusedHunk, FocusedLine) = (file, hunk, line);
                return;
            }
        }
    }

    /// <summary>The focus key of a line: "path:+11" added, "path:-11" removed, "path:10" unchanged.</summary>
    /// <param name="line">The focused line.</param>
    /// <returns>The key, or empty when no file has focus.</returns>
    internal static string KeyOf(ReviewDiffLineViewModel line) => line.Key;

    private void Focus(ReviewDiffFileViewModel file, ReviewDiffHunkViewModel? hunk) => (FocusedFile, FocusedHunk, FocusedLine) = (file, hunk, hunk is { Lines: [var line, ..] } ? line : null);

    private void OnGroupChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(ReviewDiffGroupViewModel.IsFolded), StringComparison.Ordinal))
        {
            return;
        }

        OnChanged(nameof(VisibleFiles));
        if (FocusedFile is { } file && !VisibleFiles.Contains(file))
        {
            (FocusedFile, FocusedHunk, FocusedLine) = (null, null, null);
        }
    }
}

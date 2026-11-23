namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>The diff, the page's right pane (review page spec §6.2, §6.3): its state and words, the file count and the folded or open groups.</summary>
internal sealed class ReviewDiffViewModel : ObservableObject
{
    private readonly ReviewDiffRowList _rows = [];

#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>"Files changed".</summary>
    public string TitleText => Labels.ReviewDiffTitle;

    /// <summary>The keys line: "j/k file · n/p hunk · c fix task · o fold".</summary>
    public string KeysText => Labels.ReviewDiffKeys;
#pragma warning restore CA1822, MA0041

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

    /// <summary>
    /// The diff as one flat list in reading order (#2776): group headers, then the open groups' file headers, hunk headers and lines. The view draws it in a
    /// virtualizing panel, so only the rows in view are built; it is built again when a group folds or opens and when a deleted file's contents show.
    /// </summary>
    public IReadOnlyList<ReviewDiffRow> Rows => _rows;

    /// <summary>The index in <see cref="Rows"/> of the row a focus key names (a line, a file header, a group header); -1 when no row has it.</summary>
    /// <param name="region">The region.</param>
    /// <param name="key">The key.</param>
    /// <returns>The index or -1.</returns>
    internal int RowOf(ReviewFocusRegion region, string key) => ReviewDiffRows.IndexOf(Rows, region, key);

    /// <summary>Back to the placeholder while the page is read.</summary>
    internal void ShowLoading()
    {
        (State, Text, CountText, CappedText, Groups) = (ReviewPanelState.Loading, Labels.ReviewDiffReading, string.Empty, string.Empty, []);
        _rows.ResetTo([]);
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
            foreach (var file in group.Files)
            {
                file.PropertyChanged += OnFileChanged;
            }
        }

        (State, Text, Groups) = (state, diff.Text ?? string.Empty, groups);
        _rows.ResetTo(ReviewDiffRows.Of(groups));
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
        if (at < 0 && FocusedFile is { } focused)
        {
            at = NextFrom(hunks, focused, delta);   // a focused file without lines: start from where it sits
            if (at < 0)
            {
                return;
            }
        }
        else
        {
            at = Math.Clamp(at < 0 ? 0 : at + delta, 0, hunks.Count - 1);
        }

        var (file, hunk) = hunks[at];
        Focus(file, hunk);
    }

    // The first hunk after (delta 1) or the last hunk before (delta -1) a file that has none; -1 when there is none that way.
    private int NextFrom(List<(ReviewDiffFileViewModel File, ReviewDiffHunkViewModel Hunk)> hunks, ReviewDiffFileViewModel from, int delta)
    {
        var files = VisibleFiles.ToList();
        var position = files.IndexOf(from);
        return delta > 0 ? hunks.FindIndex(h => files.IndexOf(h.File) > position) : hunks.FindLastIndex(h => files.IndexOf(h.File) < position);
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

        if (sender is ReviewDiffGroupViewModel group)
        {
            Refold(group);
        }

        OnChanged(nameof(VisibleFiles));
        if (FocusedFile is { } file && !VisibleFiles.Contains(file))
        {
            (FocusedFile, FocusedHunk, FocusedLine) = (null, null, null);
        }
    }

    // Show on a deleted file swaps its hunks in: the flat list gets its lines.
    private void OnFileChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not ReviewDiffFileViewModel file || !string.Equals(e.PropertyName, nameof(ReviewDiffFileViewModel.Hunks), StringComparison.Ordinal))
        {
            return;
        }

        var at = IndexOf(r => r is ReviewDiffFileRow row && ReferenceEquals(row.File, file), 0);
        if (at >= 0)
        {
            _rows.Replace(at, End(at + 1, static r => r is ReviewDiffFileRow or ReviewDiffGroupRow) - at, ReviewDiffRows.File(file));
        }
    }

    // A group folded or opened: only the rows under its header change; the header row (and its focus) stays.
    private void Refold(ReviewDiffGroupViewModel group)
    {
        var at = IndexOf(r => r is ReviewDiffGroupRow row && ReferenceEquals(row.Group, group), 0);
        if (at >= 0)
        {
            _rows.Replace(at + 1, End(at + 1, static r => r is ReviewDiffGroupRow) - at - 1, ReviewDiffRows.Body(group));
        }
    }

    private int IndexOf(Func<ReviewDiffRow, bool> match, int from)
    {
        for (var i = from; i < _rows.Count; i++)
        {
            if (match(_rows[i]))
            {
                return i;
            }
        }

        return -1;
    }

    // Where a run of rows ends: the first row from `from` that `stop` matches, or the end of the list.
    private int End(int from, Func<ReviewDiffRow, bool> stop) => IndexOf(stop, from) is var at and >= 0 ? at : _rows.Count;
}

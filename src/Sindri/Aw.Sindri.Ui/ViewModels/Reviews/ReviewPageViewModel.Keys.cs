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
        if (!focusInDiff || textBoxHasFocus)
        {
            return false;
        }

        return key switch
        {
            "o" => ToggleGroup(onlyIfFolded: false),
            "Enter" => ToggleGroup(onlyIfFolded: true),
            "c" => RequestLineFix(),
            _ => Navigate(key),
        };
    }

    /// <summary>Send as fix task on a failed required check: its thread is a comment in the review body.</summary>
    /// <param name="checkId">The check's id.</param>
    internal void RequestCheckFix(string checkId)
    {
        var check = Panels.SelectMany(p => p.Checks).FirstOrDefault(c => c.CanSendFix && string.Equals(c.Id, checkId, StringComparison.Ordinal));
        if (check is not null)
        {
            Ask(new ReviewFixSource(ReviewFixKind.Check, check.Title, check.Id, check.Detail, string.Empty, Line: null, Added: false), "requiredChecks:" + checkId);
        }
    }

    /// <summary>Send as fix task on an unclaimed requirement of the doc map: its thread is a comment in the review body.</summary>
    /// <param name="itemId">The requirement's id.</param>
    internal void RequestRequirementFix(string itemId)
    {
        var item = Panels.SelectMany(p => p.DocItems).SelectMany(Flatten).FirstOrDefault(i => i.CanSendFix && string.Equals(i.Id, itemId, StringComparison.Ordinal));
        if (item is not null)
        {
            Ask(new ReviewFixSource(ReviewFixKind.Requirement, item.Title, item.Id, string.Empty, string.Empty, Line: null, Added: false), "docMap:" + itemId);
        }
    }

    /// <summary>The fix-task sheet closed (Esc, Cancel, Close): focus goes back to its source, or to the strip's first part when the source is gone after a reload.</summary>
    /// <param name="returnTo">Where the request came from.</param>
    internal void ReturnFromFix(ReviewFocus returnTo) => Focus = Exists(returnTo) ? returnTo : new ReviewFocus(ReviewFocusRegion.StripFirst);

    /// <summary>A check, a requirement or a diff line asked for a fix task: the shell opens the sheet.</summary>
    internal event EventHandler<ReviewFixRequestedEventArgs>? FixRequested;

    // j, k, n and p move the diff's focus; focus then lands on the focused line.
    private bool Navigate(string key)
    {
        switch (key)
        {
            case "j" or "k":
                Diff.MoveFile(string.Equals(key, "j", StringComparison.Ordinal) ? 1 : -1);
                break;
            case "n" or "p":
                Diff.MoveHunk(string.Equals(key, "n", StringComparison.Ordinal) ? 1 : -1);
                break;
            default:
                return false;
        }

        Focus = Diff.FocusedLine is { } line ? new ReviewFocus(ReviewFocusRegion.DiffLine, ReviewDiffViewModel.KeyOf(line)) : ReviewFocus.None;
        return true;
    }

    private static IEnumerable<ReviewDocItemViewModel> Flatten(ReviewDocItemViewModel item) => item.Children.SelectMany(Flatten).Prepend(item);

    private void Ask(ReviewFixSource source, string row)
    {
        if (FixTarget is { } target)
        {
            FixRequested?.Invoke(this, new ReviewFixRequestedEventArgs(source, target, new ReviewFocus(ReviewFocusRegion.Panel, row)));
        }
    }

    private bool RequestLineFix()
    {
        if (Diff.FocusedLine is not { } line || Diff.FocusedFile is not { } file || FixTarget is not { } target)
        {
            return false;
        }

        var source = new ReviewFixSource(ReviewFixKind.Line, line.Text, string.Empty, string.Empty, file.Path, line.Number, line.Kind == ReviewDiffLineKind.Added);
        FixRequested?.Invoke(this, new ReviewFixRequestedEventArgs(source, target, new ReviewFocus(ReviewFocusRegion.DiffLine, ReviewDiffViewModel.KeyOf(line))));
        return true;
    }

    // o folds or unfolds the group whose header has focus, else the focused file's; Enter only opens a folded header. Focus lands on the header.
    private bool ToggleGroup(bool onlyIfFolded)
    {
        var group = Focus.Region == ReviewFocusRegion.GroupHeader
            ? Diff.Groups.FirstOrDefault(g => string.Equals(g.Name, Focus.Key, StringComparison.Ordinal))
            : onlyIfFolded ? null : Diff.Groups.FirstOrDefault(g => Diff.FocusedFile is { } file && g.Files.Contains(file));
        if (group is null || (onlyIfFolded && !group.IsFolded))
        {
            return false;
        }

        group.IsFolded = !group.IsFolded;
        Focus = new ReviewFocus(ReviewFocusRegion.GroupHeader, group.Name);
        return true;
    }

    private bool Exists(ReviewFocus focus) => focus.Region switch
    {
        ReviewFocusRegion.Panel => focus.Key.Split(':') is [var panel, var row] ? RowExists(panel, row) : Panels.Any(p => string.Equals(p.Id, focus.Key, StringComparison.Ordinal)),
        ReviewFocusRegion.DiffLine => Diff.VisibleFiles.Any(f => f.Hunks.Any(h => h.Lines.Any(l => string.Equals(l.Key, focus.Key, StringComparison.Ordinal)))),
        ReviewFocusRegion.GroupHeader => Diff.Groups.Any(g => string.Equals(g.Name, focus.Key, StringComparison.Ordinal)),
        ReviewFocusRegion.None or ReviewFocusRegion.StripFirst or ReviewFocusRegion.FixText or ReviewFocusRegion.FixOutcome => true,
        _ => false,
    };

    private bool RowExists(string panel, string row) =>
        Panels.Any(p => string.Equals(p.Id, panel, StringComparison.Ordinal) && (p.Checks.Any(c => string.Equals(c.Id, row, StringComparison.Ordinal)) || p.DocItems.SelectMany(Flatten).Any(i => string.Equals(i.Id, row, StringComparison.Ordinal))));
}

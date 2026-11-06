namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>One line of a hunk (review page spec §6.4): a list item whose name reads "line 42, added: text".</summary>
internal sealed class ReviewDiffLineViewModel : ObservableObject
{
    internal ReviewDiffLineViewModel(ReviewDiffLineKind kind, int? oldNumber, int? newNumber, string text)
    {
        (Kind, OldNumber, NewNumber, Text) = (kind, oldNumber, newNumber, text);
        Sign = kind switch
        {
            ReviewDiffLineKind.Added => "+",
            ReviewDiffLineKind.Removed => "-",
            ReviewDiffLineKind.Context => string.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, message: null),
        };
        AutomationName = Labels.ReviewLineName(Number, kind, text);
    }

    /// <summary>Context, added or removed.</summary>
    public ReviewDiffLineKind Kind { get; }

    /// <summary>The line number in the old file; null for an added line.</summary>
    public int? OldNumber { get; }

    /// <summary>The line number in the new file; null for a removed line.</summary>
    public int? NewNumber { get; }

    /// <summary>The number the line is named by: the old one for a removed line, else the new one.</summary>
    public int Number => (Kind == ReviewDiffLineKind.Removed ? OldNumber : NewNumber) ?? 0;

    /// <summary>"+", "-" or empty: added and removed lines never rely on color alone.</summary>
    public string Sign { get; }

    /// <summary>The line's text.</summary>
    public string Text { get; }

    /// <summary>The screen-reader name.</summary>
    public string AutomationName { get; }
}

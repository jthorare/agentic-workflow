namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>One line of a hunk (review page spec §6.4): a list item whose name reads "line 42, added: text".</summary>
internal sealed class ReviewDiffLineViewModel : ObservableObject
{
    /// <summary>Context, added or removed.</summary>
    public ReviewDiffLineKind Kind { get; }

    /// <summary>The line number in the old file; null for an added line.</summary>
    public int? OldNumber { get; }

    /// <summary>The line number in the new file; null for a removed line.</summary>
    public int? NewNumber { get; }

    /// <summary>"+", "-" or empty: added and removed lines never rely on color alone.</summary>
    public string Sign { get; } = string.Empty;

    /// <summary>The line's text.</summary>
    public string Text { get; } = string.Empty;

    /// <summary>The screen-reader name.</summary>
    public string AutomationName { get; } = string.Empty;
}

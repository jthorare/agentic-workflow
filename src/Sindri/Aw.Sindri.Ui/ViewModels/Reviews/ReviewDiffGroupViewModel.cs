namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>A group of files (review page spec §6.4, §16): folded or open, named for a screen reader as "Lockfiles, collapsed, 14 files".</summary>
internal sealed class ReviewDiffGroupViewModel : ObservableObject
{
    /// <summary>The group's name.</summary>
    public string Name { get; } = string.Empty;

    /// <summary>True while the group is folded: its files are not in the list.</summary>
    public bool IsFolded { get; set; }

    /// <summary>How many files the group holds.</summary>
    public int FileCount { get; }

    /// <summary>The screen-reader name.</summary>
    public string AutomationName { get; } = string.Empty;

    /// <summary>The group's files.</summary>
    public IReadOnlyList<ReviewDiffFileViewModel> Files { get; } = [];
}

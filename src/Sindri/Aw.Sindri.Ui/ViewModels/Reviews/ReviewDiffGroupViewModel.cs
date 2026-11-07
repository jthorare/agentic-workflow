using Aw.Sindri.Ui.Api.Reviews;

namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>A group of files (review page spec §6.4, §16): folded or open, named for a screen reader as "Lockfiles, collapsed, 14 files".</summary>
internal sealed class ReviewDiffGroupViewModel : ObservableObject
{
    internal ReviewDiffGroupViewModel(ReviewDiffGroupView group)
    {
        (Name, Files) = (group.Name, [.. group.Files.Select(f => new ReviewDiffFileViewModel(f))]);
        IsFolded = group.Folded;
    }

    /// <summary>The group's name.</summary>
    public string Name { get; }

    /// <summary>True while the group is folded: its files are not in the list.</summary>
    public bool IsFolded
    {
        get;
        set
        {
            Set(ref field, value);
            OnChanged(nameof(AutomationName));
            OnChanged(nameof(IsOpen));
        }
    }

    /// <summary>True when the group's files show.</summary>
    public bool IsOpen => !IsFolded;

    /// <summary>How many files the group holds.</summary>
    public int FileCount => Files.Count;

    /// <summary>The screen-reader name.</summary>
    public string AutomationName => IsFolded ? Labels.ReviewGroupFolded(Name, FileCount) : Labels.ReviewGroupOpen(Name, FileCount);

    /// <summary>The group's files.</summary>
    public IReadOnlyList<ReviewDiffFileViewModel> Files { get; }
}

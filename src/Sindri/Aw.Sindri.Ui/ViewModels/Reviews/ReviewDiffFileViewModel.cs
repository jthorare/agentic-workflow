namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>One file of the diff (review page spec §6.2, §16): its status, its note (a rename, a deleted, binary or too-large file) and its hunks.</summary>
internal sealed class ReviewDiffFileViewModel : ObservableObject
{
    /// <summary>The file's path.</summary>
    public string Path { get; } = string.Empty;

    /// <summary>added, modified, renamed or deleted.</summary>
    public string Status { get; } = string.Empty;

    /// <summary>"+4 −1".</summary>
    public string Stat { get; } = string.Empty;

    /// <summary>The note above the patch: "Renamed from OLD, 91% the same", "Deleted file: 61 lines removed. Contents hidden.", "Binary file, not shown here.", "Too large to show here"; empty for none.</summary>
    public string Note { get; } = string.Empty;

    /// <summary>True while a deleted file's contents are held back; <see cref="Show"/> reveals them.</summary>
    public bool IsContentsHidden { get; }

    /// <summary>True when the note has a Show button (a deleted file).</summary>
    public bool HasShow { get; }

    /// <summary>True when the note has an Open on GitHub button (a binary or too-large file).</summary>
    public bool HasOpenOnGitHub { get; }

    /// <summary>The file on GitHub.</summary>
    public string Url { get; } = string.Empty;

    /// <summary>The hunks shown: none while the contents are hidden or the patch is left out.</summary>
    public IReadOnlyList<ReviewDiffHunkViewModel> Hunks { get; } = [];

    /// <summary>Show on a deleted file: reveals its hunks.</summary>
    internal void Show() => OnChanged(nameof(IsContentsHidden));
}

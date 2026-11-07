using Aw.Sindri.Ui.Api.Reviews;

namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>One file of the diff (review page spec §6.2, §16): its status, its note (a rename, a deleted, binary or too-large file) and its hunks.</summary>
internal sealed class ReviewDiffFileViewModel : ObservableObject
{
    private readonly string? _patch;

    internal ReviewDiffFileViewModel(ReviewDiffFileView file)
    {
        (Path, Status, Note, Url, _patch) = (file.Path, file.Status, file.Note ?? string.Empty, file.Url, file.Patch);
        Stat = Labels.ReviewStat(file.Additions, file.Deletions);
        (ShowName, OpenName) = (Labels.ReviewShowFor(file.Path), Labels.ReviewOpenFileFor(file.Path));
        var deleted = string.Equals(file.Omitted, "deleted", StringComparison.Ordinal);
        (IsContentsHidden, HasShow) = (deleted, deleted);
        HasOpenOnGitHub = file.Omitted is { } omitted && !string.Equals(omitted, "deleted", StringComparison.Ordinal);
        Hunks = IsContentsHidden || HasOpenOnGitHub || _patch is null ? [] : ReviewPatch.Hunks(_patch, Path);
    }

    /// <summary>The file's path.</summary>
    public string Path { get; }

    /// <summary>added, modified, renamed or deleted.</summary>
    public string Status { get; }

#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>"Show", the button's words.</summary>
    public string ShowText => Labels.ReviewDiffShow;
#pragma warning restore CA1822, MA0041

#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>"Open on GitHub ↗", the button's words.</summary>
    public string OpenText => Labels.ReviewOpenOnGitHubArrow;
#pragma warning restore CA1822, MA0041

    /// <summary>The Show button's screen-reader name: "Show src/X.cs".</summary>
    public string ShowName { get; }

    /// <summary>The Open on GitHub button's screen-reader name.</summary>
    public string OpenName { get; }

    /// <summary>"+4 −1".</summary>
    public string Stat { get; }

    /// <summary>The note above the patch; empty for none.</summary>
    public string Note { get; }

    /// <summary>True while a deleted file's contents are held back; <see cref="Show"/> reveals them.</summary>
    public bool IsContentsHidden { get; private set => Set(ref field, value); }

    /// <summary>True when the note has a Show button (a deleted file whose contents are hidden).</summary>
    public bool HasShow { get; private set => Set(ref field, value); }

    /// <summary>True when the note has an Open on GitHub button (a binary or too-large file).</summary>
    public bool HasOpenOnGitHub { get; }

    /// <summary>The file on GitHub.</summary>
    public string Url { get; }

    /// <summary>The hunks shown: none while the contents are hidden or the patch is left out.</summary>
    public IReadOnlyList<ReviewDiffHunkViewModel> Hunks { get; private set => Set(ref field, value); }

    /// <summary>Show on a deleted file: reveals its hunks.</summary>
    internal void Show()
    {
        IsContentsHidden = false;
        HasShow = false;
        Hunks = _patch is null ? [] : ReviewPatch.Hunks(_patch, Path);
    }
}

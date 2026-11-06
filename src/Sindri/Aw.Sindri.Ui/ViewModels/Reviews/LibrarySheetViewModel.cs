using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Reviews;

namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>The library errors sheet (review page spec §6.1): docs with errors, then near misses, then unreadable folders, and the served cap line at the end.</summary>
internal sealed class LibrarySheetViewModel(ISindriApi api) : ObservableObject
{
    private readonly ISindriApi _api = api;

#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>The sheet's title.</summary>
    public string Title => Labels.LibrarySheetTitle;

    /// <summary>The close button's word.</summary>
    public string CloseText => Labels.LibraryClose;

    /// <summary>The words under the title while the read is out.</summary>
    public string ReadingText => Labels.ReadingLibrary;

    /// <summary>The words when there is nothing to fix.</summary>
    public string CleanText => Labels.LibraryClean;
#pragma warning restore CA1822, MA0041

    /// <summary>True while the sheet is open.</summary>
    public bool IsOpen { get; private set => Set(ref field, value); }

    /// <summary>True until the read answers.</summary>
    public bool IsLoading { get; private set => Set(ref field, value); }

    /// <summary>The counts in words: "41 docs, 3 with errors", "33 docs, 1 folder can't be read".</summary>
    public string Summary { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The sections that have entries, in order.</summary>
    public IReadOnlyList<LibrarySectionViewModel> Sections { get; private set => Set(ref field, value); } = [];

    /// <summary>The served line when the lists are cut short; null otherwise.</summary>
    public string? CappedText { get; private set => Set(ref field, value); }

    /// <summary>True when there is nothing to fix.</summary>
    public bool IsClean { get; private set => Set(ref field, value); }

    /// <summary>The last read's failure line; empty when it worked.</summary>
    public string Error { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Opens the sheet and reads the library.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the answer is shown.</returns>
    internal async Task Open(Uri url, CancellationToken cancellationToken)
    {
        (IsOpen, IsLoading, Error) = (true, true, string.Empty);
        var read = await _api.Library(url, cancellationToken).ConfigureAwait(true);
        IsLoading = false;
        if (read.IsError)
        {
            Error = Labels.LibraryFailed(read.Error);
            return;
        }

        Show(read.Value);
    }

    private void Show(LibraryView library)
    {
        Summary = Labels.LibrarySummary(library.Docs, library.WithErrors, library.UnreadableRoots.Count);
        List<LibrarySectionViewModel> sections = [];
        if (library.Broken.Count > 0)
        {
            sections.Add(new(Labels.LibraryBrokenTitle, [.. library.Broken.Select(static b => new LibraryItemViewModel(b.Doc, b.Path, [.. b.Errors.Select(static e => $"{e.Rule}: {e.Text}")]))]));
        }

        if (library.NearMisses.Count > 0)
        {
            sections.Add(new(Labels.LibraryNearMissTitle, [.. library.NearMisses.Select(static n => new LibraryItemViewModel(n.Path, string.Empty, [$"{n.Rule}: {n.Text}"]))]));
        }

        if (library.UnreadableRoots.Count > 0)
        {
            sections.Add(new(Labels.LibraryRootsTitle, [.. library.UnreadableRoots.Select(static r => new LibraryItemViewModel(r.Root, string.Empty, [r.Reason]))]));
        }

        (Sections, CappedText, IsClean) = (sections, library.CappedText, sections.Count == 0);
    }

    /// <summary>Closes the sheet.</summary>
    internal void Close() => IsOpen = false;
}

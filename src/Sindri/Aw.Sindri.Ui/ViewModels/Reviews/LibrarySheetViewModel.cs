using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>The library errors sheet (review page spec §6.1): docs with errors, then near misses, then unreadable folders, and the served cap line at the end.</summary>
internal sealed class LibrarySheetViewModel(ISindriApi api) : ObservableObject
{
    private readonly ISindriApi _api = api;

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
    internal Task Open(Uri url, CancellationToken cancellationToken)
    {
        _ = (_api, url, cancellationToken);   // red: Ph7 part 3 fills it
        return Task.CompletedTask;
    }

    /// <summary>Closes the sheet.</summary>
    internal void Close() => IsOpen = false;
}

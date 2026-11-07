namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>One section of the library errors sheet.</summary>
/// <param name="Title">The section's heading.</param>
/// <param name="Items">Its entries, in served order.</param>
internal sealed record LibrarySectionViewModel(string Title, IReadOnlyList<LibraryItemViewModel> Items)
{
    /// <summary>How many entries.</summary>
    public int Count => Items.Count;
}

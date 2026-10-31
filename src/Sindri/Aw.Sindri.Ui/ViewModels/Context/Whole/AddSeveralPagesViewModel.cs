namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The "Add several pages" sheet (pages spec §3.6): paste a list, read it as one row per page, tick the rows, and write every ticked row in one profile write.</summary>
#pragma warning disable MA0182 // why: the Context page opens it in the next commit of this branch
internal sealed class AddSeveralPagesViewModel : ObservableObject
{
    /// <summary>Builds a sheet that has read nothing yet.</summary>
    /// <param name="writes">Where the list is read and the pages are written.</param>
    /// <param name="repo">The repo to read the list for, or null.</param>
    internal AddSeveralPagesViewModel(IPageWrites writes, string? repo)
    {
        ArgumentNullException.ThrowIfNull(writes);
        _ = repo;
    }

    /// <summary>Gets or sets the pasted list.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Gets one row per block of the last read list.</summary>
    public IReadOnlyList<AddSeveralPagesRowViewModel> Rows { get; private set; } = [];

    /// <summary>Reads the pasted list.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the rows are drawn.</returns>
    internal Task ReadList(CancellationToken cancellationToken) => throw new NotSupportedException("ReadList is not written yet (#1724 P2c, red first).");
}

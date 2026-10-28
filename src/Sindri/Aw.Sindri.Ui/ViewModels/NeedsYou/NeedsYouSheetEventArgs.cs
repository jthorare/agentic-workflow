namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>The bar asks for a row's sheet: the step needs something only the sheet can take (a file).</summary>
/// <param name="rowId">The row's id.</param>
internal sealed class NeedsYouSheetEventArgs(string rowId) : EventArgs
{
    /// <summary>The row's id.</summary>
    public string RowId { get; } = rowId;
}

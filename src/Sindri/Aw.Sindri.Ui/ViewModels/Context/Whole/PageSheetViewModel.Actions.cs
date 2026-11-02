namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The sheet's check on its action rows before a write (<see cref="PageSheetViewModel"/>, continued).</summary>
internal sealed partial class PageSheetViewModel
{
    // A saved action (it has an id) whose name was cleared would be deleted with its concept links by the write, so it asks instead (#1853);
    // an empty new row is still dropped.
    private bool ClearedSavedActions()
    {
        var cleared = false;
        foreach (var row in Actions)
        {
            var empty = row.Id is not null && row.Name.Trim().Length == 0;
            row.Refuse(empty ? Labels.PageSheetActionNameNeeded : string.Empty);
            cleared |= empty;
        }

        return cleared;
    }
}

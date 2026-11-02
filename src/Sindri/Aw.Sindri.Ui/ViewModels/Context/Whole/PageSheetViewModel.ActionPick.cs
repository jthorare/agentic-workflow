namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>An action row's "Pick another" (<see cref="PageSheetViewModel"/>, continued, pages spec §3.6): a search over the map actions of the page's map surface that repoints one row.</summary>
internal sealed partial class PageSheetViewModel
{
    /// <summary>Gets the latest action search: it ends once its answer is drawn.</summary>
    internal Task ActionPicking { get; private set; } = Task.CompletedTask;

    /// <summary>Gets the row whose search is open, or null.</summary>
    internal PageSheetActionViewModel? PickingRow { get; private set; }

    /// <summary>Opens a row's search and reads the page's actions.</summary>
    /// <param name="row">The row.</param>
    internal void OpenActionPick(PageSheetActionViewModel row) => throw new NotSupportedException();

    /// <summary>Closes the open row search without a pick.</summary>
    /// <returns>True when a search was open.</returns>
    internal bool CloseActionPick() => throw new NotSupportedException();

    /// <summary>Points the open row at a found map action and closes its search.</summary>
    /// <param name="match">The row of <see cref="PageSheetActionViewModel.PickRows"/>.</param>
    internal void ChooseAction(PageMatch match) => throw new NotSupportedException();
}

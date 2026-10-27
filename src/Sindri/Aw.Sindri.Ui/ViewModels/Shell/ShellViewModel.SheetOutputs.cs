using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.NeedsYou;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The files a desk row's Complete carries when it is pressed in the row's sheet (<see cref="ShellViewModel"/>, continued).</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>Completes the desk task of a row from its sheet: with the files the sheet's inputs hold when the task needs outputs, else as the row's own Complete does.</summary>
    /// <param name="row">The row whose sheet shows Complete.</param>
    /// <returns>A task that completes when the answer is said.</returns>
    internal async Task CompleteSheetRow(NeedsYouRowViewModel row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.SheetComplete is not { } action)
        {
            return;
        }

        if (!row.HasOutputs)
        {
            NeedsYou.Press(row.Id, action);
            return;
        }

        if (await row.ReadOutputs(CancellationToken.None).ConfigureAwait(true) is { } files)
        {
            await Dispatch(row.Id, action, files).ConfigureAwait(true);
        }
    }

    // The sheet of a desk row asks for the files its task requires: the task is read when the sheet opens, and its required outputs become the row's inputs.
    private async Task LoadSheetOutputs(NeedsYouRowViewModel? row)
    {
        if (row is not { HasOutputs: false, SheetComplete: not null } || _desk is not { } desk || row.Entry.Subject is not { Id.Length: > 0 } subject)
        {
            return;
        }

        if (await desk.Load(subject.Id).ConfigureAwait(true) is { RequiredOutputs.Count: > 0 } target)
        {
            row.SetOutputs([.. target.RequiredOutputs.Select(key => new DeskOutputViewModel(key, _files))]);
        }
    }
}

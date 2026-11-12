namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): the editor's keyboard map (spec 4.6) wired to the page. A bare letter never commits a write.</summary>
internal sealed partial class WorkflowsPageViewModel
{
    /// <summary>Does what a key press means on the editor.</summary>
    /// <param name="press">The key and the focus facts; the page knows whether its own sheet is open.</param>
    /// <param name="cancellationToken">Cancels the calls.</param>
    /// <returns>True when the key was the editor's and is handled; false lets the shell have it.</returns>
    internal async Task<bool> PressKey(EditorKeyPress press, CancellationToken cancellationToken)
    {
        if (!IsRuns)
        {
            return false;
        }

        var action = EditorKeys.Resolve(press with { SheetOpen = IsEditing });
        if (action == EditorKeyAction.AddStep && GraphEdit.CanAdd)
        {
            await GraphEdit.BeginAdd(cancellationToken).ConfigureAwait(true);
            return true;
        }

        if (action == EditorKeyAction.OpenSheet && CanEdit && !IsEditing)
        {
            await Edit(cancellationToken).ConfigureAwait(true);
            return true;
        }

        if (action == EditorKeyAction.SaveStep && IsEditing)
        {
            await Save(cancellationToken).ConfigureAwait(true);
            return true;
        }

        return action == EditorKeyAction.Close && CloseTopLayer();   // walking, fitting, rename, test and undo belong to the canvas and to later work
    }

    // Esc closes the topmost layer only: a picker, a list or a confirm, then the sheet. Nothing is saved. With nothing open the shell's own Esc runs.
    private bool CloseTopLayer()
    {
        var layer = EditorKeys.TopLayer(activateConfirm: false, picker: GraphEdit.IsAdding || GraphEdit.IsConnecting || GraphEdit.IsConfirmingRemove, sheet: IsEditing, runningTest: false);
        if (layer == EscLayer.Picker)
        {
            GraphEdit.Cancel();
            return true;
        }

        if (layer == EscLayer.Sheet)
        {
            CloseEditor();
            return true;
        }

        return false;
    }
}

using System.Text.Json;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): the undo of Save step (workflows page spec, Ctrl+Z row).</summary>
internal sealed partial class WorkflowsPageViewModel
{
    /// <summary>Raised after a step was saved: the shell shows the toast with its Undo.</summary>
    internal event EventHandler<WorkflowStepSavedEventArgs>? StepSaved;

    // The toast's Undo puts back the graph the save replaced, as a new revision; it is off while an Activate or Pause or another edit is on the way.
    private void OfferUndo(string slug, JsonElement before)
    {
        if (before.ValueKind == JsonValueKind.Object)   // no graph read, nothing to put back
        {
            StepSaved?.Invoke(this, new WorkflowStepSavedEventArgs(() => UndoSave(slug, before), () => CanUndoSave(slug)));
        }
    }

    private bool CanUndoSave(string slug) =>
        !IsChangingState && !Editor.IsSending && !Editor.NeedsReload && string.Equals(Editor.Slug, slug, StringComparison.Ordinal);

    private async Task<string?> UndoSave(string slug, JsonElement before)
    {
        if (!CanUndoSave(slug))
        {
            return Labels.WfUndoNotNow;
        }

        var failure = await Editor.Restore(slug, before, CancellationToken.None).ConfigureAwait(true);
        await RereadRuns(slug, CancellationToken.None).ConfigureAwait(true);
        return failure;
    }
}

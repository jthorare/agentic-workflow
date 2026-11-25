using System.Text.Json;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): the undo of Save step (workflows page spec, Ctrl+Z row).</summary>
internal sealed partial class WorkflowsPageViewModel
{
    /// <summary>Raised after a step was saved: the shell shows the toast with its Undo.</summary>
    internal event EventHandler<WorkflowStepSavedEventArgs>? StepSaved;

    /// <summary>Raised after an undo was saved and the page read the workflow again: the reload rebuilt the sheet's fields and the graph, so the view puts focus back where the person was.</summary>
    internal event EventHandler? StepUndone;

    // The toast's Undo puts back the graph the save replaced, as a new revision on the revision the save made (the editor's base right after it), so a newer
    // edit is never overwritten: the server refuses a stale base, and the Undo is off as soon as the editor's base moves on (a canvas edit, a reload that found more).
    // It is also off while an Activate or Pause or another edit is on the way.
    private void OfferUndo(string slug, JsonElement before)
    {
        if (before.ValueKind == JsonValueKind.Object)   // no graph read, nothing to put back
        {
            var saved = Editor.BaseRevision;
            StepSaved?.Invoke(this, new WorkflowStepSavedEventArgs(() => UndoSave(slug, before, saved), () => CanUndoSave(slug, saved)));
        }
    }

    private bool CanUndoSave(string slug, int saved) =>
        !IsChangingState && !Editor.IsSending && !Editor.NeedsReload && string.Equals(Editor.Slug, slug, StringComparison.Ordinal) && Editor.BaseRevision == saved;

    private async Task<string?> UndoSave(string slug, JsonElement before, int saved)
    {
        if (!CanUndoSave(slug, saved))
        {
            return Labels.WfUndoNotNow;
        }

        var failure = await Editor.Restore(slug, before, saved, CancellationToken.None).ConfigureAwait(true);
        await RereadRuns(slug, CancellationToken.None).ConfigureAwait(true);
        if (failure is null)
        {
            NoteSaved();   // the undo made a new revision: the line names it
            _undoneRevision = Runs.Shown?.LatestRevision ?? 0;   // the revision the undo wrote
            NotifySaved();
            StepUndone?.Invoke(this, EventArgs.Empty);
        }

        return failure;
    }
}

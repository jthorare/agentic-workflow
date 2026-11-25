using System.Text.Json;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): the undo of Save step (workflows page spec, Ctrl+Z row).</summary>
internal sealed partial class WorkflowsPageViewModel
{
    /// <summary>Raised after a step was saved: the shell shows the toast with its Undo.</summary>
    internal event EventHandler<WorkflowStepSavedEventArgs>? StepSaved;

    // The toast's Undo puts back the graph the save replaced, as a new revision on the revision the save made (its answer's), so a newer
    // edit is never overwritten: the server refuses a stale base, and the Undo is off as soon as the editor's base moves on (a canvas edit, a reload that found more).
    // It is also off while an Activate or Pause or another edit is on the way.
    private void OfferUndo(string slug, JsonElement before)
    {
        if (before.ValueKind == JsonValueKind.Object)   // no graph read, nothing to put back
        {
            var saved = Editor.SavedRevision;   // the save's own answer, not the base the reload read: an edit in between must turn Undo off
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
        return failure;
    }
}

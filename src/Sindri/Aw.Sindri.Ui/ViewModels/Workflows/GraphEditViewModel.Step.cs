using Aw.Sindri.Ui.Api.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The graph edits (<see cref="GraphEditViewModel"/>, continued): sheet-first Add step. Picking a block opens the form of a step that does not exist yet; Save sends one add_node.</summary>
internal sealed partial class GraphEditViewModel
{
    /// <summary>Gets or sets the layout the workflow.editor key picked: the sheet layout (the default) opens a form for a new step; the steps layout adds it at once.</summary>
    internal WorkflowEditorLayout Layout { get; set; } = WorkflowEditorLayout.Sheet;

    /// <summary>Raised when the form of a new step is ready: the page opens its sheet.</summary>
    internal event EventHandler? StepSheetOpened;

    /// <summary>Picks a block in the picker: opens its form (sheet layout) or adds the step at once (steps layout).</summary>
    /// <param name="block">The block.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the form is open or the answer is shown.</returns>
    internal Task PickBlock(WorkflowBlock block, CancellationToken cancellationToken) =>
        Layout == WorkflowEditorLayout.Steps ? AddStep(block, cancellationToken) : BeginStep(block, cancellationToken);

    /// <summary>Opens the form of a step that does not exist yet. Nothing is added until <see cref="SaveStep"/>.</summary>
    /// <param name="block">The block.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the form is open.</returns>
    internal async Task BeginStep(WorkflowBlock block, CancellationToken cancellationToken)
    {
        if (!CanAdd || !await EnsureLoaded(cancellationToken).ConfigureAwait(true) || !StillSame)
        {
            return;
        }

        Editor.BeginNew(block);
        SetMode(Mode.None);
        StepSheetOpened?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Sends the step being made as one add_node (and a connect after the selected node), then reads the workflow again. An empty required field is refused in its field.</summary>
    /// <param name="cancellationToken">Cancels the calls.</param>
    /// <returns>True when the workflow took the step.</returns>
    internal async Task<bool> SaveStep(CancellationToken cancellationToken)
    {
        if (Editor.PendingBlock is not { } block || _url() is null || _runs.Slug.Length == 0 || _runs.Graph is null || _runs.Shown is not { ArchivedAt: null }
            || Editor.IsSending || Editor.NeedsReload || !string.Equals(Editor.Slug, _runs.Slug, StringComparison.Ordinal))
        {
            return false;
        }

        var (slug, after) = (_runs.Slug, _runs.Graph.SelectedId);
        var sent = await Editor.SaveNew(FreshId(block.Type), after, cancellationToken).ConfigureAwait(true);
        if (sent)
        {
            await ReadPage(slug, cancellationToken).ConfigureAwait(true);
        }

        return sent;
    }
}

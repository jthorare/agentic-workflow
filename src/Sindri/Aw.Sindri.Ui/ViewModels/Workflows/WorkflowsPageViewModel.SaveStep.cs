namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): the two ways Save step sends an edit, each offering the Undo toast once it made a revision.</summary>
internal sealed partial class WorkflowsPageViewModel
{
    private async Task SaveNewStep(CancellationToken cancellationToken)
    {
        var (slug, specBefore) = (Runs.Slug, Editor.SpecRead);
        var added = await Editor.AddPendingStep(cancellationToken).ConfigureAwait(true);
        await RereadRuns(slug, cancellationToken).ConfigureAwait(true);
        if (added)
        {
            CloseSettings();   // the step exists now; a refusal keeps the sheet open with the refusal shown and every typed value
            OfferUndo(slug, specBefore);
        }
    }

    // Sends every changed field in ONE call on the revision read.
    private async Task SaveFields(CancellationToken cancellationToken)
    {
        var changed = Editor.Fields.Where(f => f.IsChanged).Select(f => (f.Name, f.Text)).ToList();
        var (slug, before, specBefore) = (Runs.Slug, Editor.BaseRevision, Editor.SpecRead);
        await Editor.EditFields(changed, cancellationToken).ConfigureAwait(true);
        await RereadRuns(slug, cancellationToken).ConfigureAwait(true);   // the page shows the revision Sindri holds now, so a graph edit next is not held as "never showed"
        if (!string.Equals(Runs.Slug, slug, StringComparison.Ordinal) || Editor.BaseRevision <= before)
        {
            return;
        }

        if (Runs.Shown is { } shown && shown.LatestRevision == Editor.BaseRevision)
        {
            NoteSaved();
        }

        OfferUndo(slug, specBefore);   // the save made a revision: the toast offers to put the graph before it back
    }
}

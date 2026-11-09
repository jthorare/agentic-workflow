#pragma warning disable CA1822, MA0041 // red stubs; the real members read instance state
namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): the editor sheet (#2258 PR 17 part 3a).</summary>
internal sealed partial class WorkflowsPageViewModel
{
    /// <summary>The editor over the page's API.</summary>
    internal OpsEditor Editor { get; }

    /// <summary>True while the editor sheet is open.</summary>
    public bool IsEditing => throw new NotSupportedException();

    /// <summary>True when the open workflow can be edited: a workflow is open and it is not archived.</summary>
    public bool CanEdit => throw new NotSupportedException();

    /// <summary>The sheet's heading: the workflow and the revision the edits are sent on.</summary>
    public string EditorHeading => throw new NotSupportedException();

    /// <summary>"Save", or "Saving..." while an edit is on the way.</summary>
    public string SaveText => throw new NotSupportedException();

    /// <summary>True when Save does something: the form has fields, no edit is on the way and no reload is due.</summary>
    public bool CanSave => throw new NotSupportedException();

    /// <summary>Opens the sheet on the selected graph node's fields.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the form is made.</returns>
    internal Task Edit(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Sends the fields that changed, one edit each, on the revision read.</summary>
    /// <param name="cancellationToken">Cancels the calls.</param>
    /// <returns>A task that completes when the answers are shown.</returns>
    internal Task Save(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Closes the sheet.</summary>
    internal void CloseEditor() => throw new NotSupportedException();

    private void WatchEditor() => _ = Editor;
}

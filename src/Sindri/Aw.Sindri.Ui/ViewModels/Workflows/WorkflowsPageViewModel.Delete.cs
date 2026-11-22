namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): Delete, from the header's ⋯ menu (Workflows page spec §4.9). A stub until its phase builds it.</summary>
internal sealed partial class WorkflowsPageViewModel
{
    /// <summary>True when Delete is offered.</summary>
    public bool CanDelete => false;

    /// <summary>True while the confirm is open.</summary>
    public bool IsConfirmingDelete => false;

    /// <summary>True for the off form.</summary>
    public bool IsDeleteOffForm => false;

    /// <summary>True for the active form.</summary>
    public bool IsDeleteActiveForm => false;

    /// <summary>True for the in-flight form.</summary>
    public bool IsDeleteInFlightForm => false;

    /// <summary>The confirm's sentence.</summary>
    public string DeleteConfirmText => string.Empty;

    /// <summary>The red button's words.</summary>
    public string DeleteYesText => string.Empty;

    /// <summary>True while the call is on the way.</summary>
    public bool IsDeleting => false;

    /// <summary>A refusal in the confirm.</summary>
    public string DeleteNoteText => string.Empty;

    /// <summary>True when the confirm holds a refusal.</summary>
    public bool HasDeleteNote => false;

    /// <summary>The notice after a delete.</summary>
    public string DeletedNoticeText => string.Empty;

    /// <summary>True when the list shows the notice.</summary>
    public bool HasDeletedNotice => false;

    /// <summary>The run that is still going.</summary>
    public string DeleteRunId => string.Empty;

    /// <summary>Opens the confirm.</summary>
    internal void RequestDelete()
    {
    }

    /// <summary>Closes the confirm.</summary>
    internal void CancelDelete()
    {
    }

    /// <summary>Deletes.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task.</returns>
    internal Task ConfirmDelete(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Pauses from the active form.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task.</returns>
    internal Task PauseFromDelete(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Opens the run that is still going.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task.</returns>
    internal Task OpenDeleteRun(CancellationToken cancellationToken) => Task.CompletedTask;
}

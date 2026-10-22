namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The list's column heads (<see cref="WorkflowsViewModel"/>, continued; F3 workflows spec §2 "List", §6).</summary>
internal sealed partial class WorkflowsViewModel
{
#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>The Item column's head.</summary>
    public string ColItemText => Labels.WorkflowsColItem;

    /// <summary>The Stage column's head.</summary>
    public string ColStageText => Labels.WorkflowsColStage;

    /// <summary>The Status column's head.</summary>
    public string ColStatusText => Labels.WorkflowsColStatus;

    /// <summary>The Step column's head.</summary>
    public string ColStepText => Labels.WorkflowsColStep;

    /// <summary>The Waiting on column's head.</summary>
    public string ColWaitingOnText => Labels.WorkflowsColWaitingOn;

    /// <summary>The Updated column's head.</summary>
    public string ColUpdatedText => Labels.WorkflowsColUpdated;
#pragma warning restore CA1822, MA0041
}

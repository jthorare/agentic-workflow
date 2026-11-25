namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): the undo of Save step (workflows page spec, Ctrl+Z row).</summary>
internal sealed partial class WorkflowsPageViewModel
{
    /// <summary>Raised after a step was saved: the shell shows the toast with its Undo.</summary>
    internal event EventHandler<WorkflowStepSavedEventArgs>? StepSaved;

    // Not yet raised: Save step does not offer the undo until the fix.
    internal void RaiseStepSaved(WorkflowStepSavedEventArgs e) => StepSaved?.Invoke(this, e);
}

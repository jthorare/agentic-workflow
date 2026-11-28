namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>A step was saved: the shell shows "Step saved. Undo (Ctrl+Z)" and runs <see cref="Undo"/> when the person asks for it.</summary>
/// <param name="undo">Restores the graph the save replaced as a new revision; answers null when it worked, else the reason it did not.</param>
/// <param name="canUndo">False while an Activate or Pause is on the way or an edit is being sent: the toast's Undo is off then.</param>
internal sealed class WorkflowStepSavedEventArgs(Func<Task<string?>> undo, Func<bool> canUndo) : EventArgs
{
    /// <summary>Restores the graph the save replaced as a new revision; answers null when it worked, else the reason it did not.</summary>
    internal Func<Task<string?>> Undo { get; } = undo;

    /// <summary>False while an Activate or Pause is on the way or an edit is being sent.</summary>
    internal Func<bool> CanUndo { get; } = canUndo;
}

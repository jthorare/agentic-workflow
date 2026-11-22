namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): the server's validation findings (#3803). Stub until the slice lands.</summary>
internal sealed partial class WorkflowsPageViewModel
{
    /// <summary>The open workflow's findings.</summary>
    public WorkflowFindingsViewModel Findings { get; } = new();

    /// <summary>The findings of the selected step.</summary>
    public IReadOnlyList<WorkflowFindingViewModel> StepFindings { get; } = [];

    /// <summary>The field the sheet focuses after a fix, or null.</summary>
    public string? FocusFieldName { get; } = "stub";

    /// <summary>True when a fix asked for the field picker (#3033 reads it).</summary>
    public bool IsFieldPickerAsked { get; } = true;

    /// <summary>The newest validate read.</summary>
    internal Task FindingsRead { get; } = Task.CompletedTask;

    /// <summary>Runs a finding's fix.</summary>
    /// <param name="finding">The finding.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when the sheet is open.</returns>
    internal Task FixFinding(WorkflowFindingViewModel finding, CancellationToken cancellationToken)
    {
        _ = finding.Code;   // stub: the fix lands with the slice
        return OpenSettings(cancellationToken);
    }
}

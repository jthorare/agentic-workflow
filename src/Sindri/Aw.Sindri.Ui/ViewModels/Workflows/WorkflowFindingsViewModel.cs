namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The open workflow's validation findings (#3803). Stub until the slice lands.</summary>
internal sealed class WorkflowFindingsViewModel : ObservableObject
{
    /// <summary>Every finding, in the order the server listed them.</summary>
    public IReadOnlyList<WorkflowFindingViewModel> All { get; } = [];

    /// <summary>True when the server found something.</summary>
    public bool HasFindings { get; } = true;

    /// <summary>"1 thing to fix", "3 things to fix"; empty with none.</summary>
    public string HeaderText { get; } = "stub";
}

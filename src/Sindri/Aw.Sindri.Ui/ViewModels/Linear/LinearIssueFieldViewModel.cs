namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>One cell of the issue sheet's field grid: a label and its value in words (state and priority are text, never colour alone).</summary>
/// <param name="Label">"State", "Assignee", "Priority", "Project", "Cycle" or "Labels".</param>
/// <param name="Value">The value, or a dash when the issue has none.</param>
internal sealed record LinearIssueFieldViewModel(string Label, string Value)
{
    /// <summary>The cell's accessible name: "State: In Progress".</summary>
    public string Name => Label + ": " + Value;
}

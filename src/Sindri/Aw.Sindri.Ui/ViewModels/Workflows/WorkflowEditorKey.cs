#pragma warning disable MA0182 // wired into the page and its views in the next round (needs the P0 stubs)

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The workflow.editor profile key: "sheet" or "steps". It picks the layout only; the Add step, Change trigger and name fixes ship in both.</summary>
internal static class WorkflowEditorKey
{
    /// <summary>The profile key.</summary>
    internal const string Key = "workflow.editor";

    /// <summary>Reads the key's value.</summary>
    /// <param name="value">The profile value, or null when unset.</param>
    /// <returns>The layout; an unset or unknown value gives the default.</returns>
    internal static WorkflowEditorLayout Read(string? value) => string.Equals(value, "steps", StringComparison.Ordinal) ? WorkflowEditorLayout.Steps : WorkflowEditorLayout.Sheet;

    /// <summary>Tells whether a value is one the key accepts.</summary>
    /// <param name="value">The profile value.</param>
    /// <returns>True for "sheet" and "steps".</returns>
    internal static bool IsValid(string? value) => string.Equals(value, "sheet", StringComparison.Ordinal) || string.Equals(value, "steps", StringComparison.Ordinal);
}

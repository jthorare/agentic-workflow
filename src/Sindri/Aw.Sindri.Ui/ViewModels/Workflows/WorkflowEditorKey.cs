#pragma warning disable MA0041, MA0182 // MA0041: stub bodies; MA0182: wired into the page views in the next round

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The workflow.editor profile key: sheet or steps. It picks the layout only; the Add step, Change trigger and name fixes ship in both.</summary>
internal static class WorkflowEditorKey
{
    /// <summary>The profile key.</summary>
    internal const string Key = "workflow.editor";

    /// <summary>Reads the key's value.</summary>
    /// <param name="value">The profile value, or null when unset.</param>
    /// <returns>The layout; an unset or unknown value gives the default.</returns>
    internal static WorkflowEditorLayout Read(string? value) => throw new NotSupportedException();

    /// <summary>Tells whether a value is one the key accepts.</summary>
    /// <param name="value">The profile value.</param>
    /// <returns>True for sheet and steps.</returns>
    internal static bool IsValid(string? value) => throw new NotSupportedException();
}

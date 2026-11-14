namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the workflow editor sheet (#2258 PR 17 part 3a).</summary>
internal static partial class Labels
{
    /// <summary>The Edit button on an open workflow.</summary>
    internal const string WfEdit = "Edit";

    /// <summary>The editor sheet's Save button.</summary>
    internal const string WfEditSave = "Save";

    /// <summary>The editor sheet's Save button while the edit is on the way.</summary>
    internal const string WfEditSaving = "Saving\u2026";

    /// <summary>The editor sheet's Close button.</summary>
    internal const string WfEditClose = "Close";

    /// <summary>The editor sheet's line when no node is selected, or the selected node has no settings.</summary>
    internal const string WfEditorNoFields = "Select a node on the graph that has settings, then press Edit.";

    /// <summary>The graph edit bar's line while the editor sheet is open: graph edits wait for the sheet to close.</summary>
    internal const string WfEditSheetOpenNote = "Close the editor sheet to edit steps on the graph.";

    /// <summary>The editor sheet's heading when no step is selected.</summary>
    /// <param name="name">The workflow's name.</param>
    /// <returns>The words.</returns>
    internal static string WfEditorHeading(string name) => "Edit " + name;

    /// <summary>The editor sheet's heading for the selected step: its title and what kind of step it is.</summary>
    /// <param name="title">The step's own title, or its block's title when it has none.</param>
    /// <param name="kind">"trigger", or the block's title.</param>
    /// <returns>The words.</returns>
    internal static string WfEditorStep(string title, string kind) => title + " \u00b7 " + kind;

    /// <summary>The editor sheet's heading for a step that does not exist yet.</summary>
    /// <param name="kind">The block's title.</param>
    /// <returns>The words.</returns>
    internal static string WfEditorNewStep(string kind) => $"New step: {kind}";

    /// <summary>The line shown in place of the form when a new step has no settings.</summary>
    internal const string WfEditorNewStepNoFields = "This step has no settings. Save adds it.";

    /// <summary>The Advanced toggle under a field whose presets hide its raw text.</summary>
    internal const string WfFieldAdvanced = "Advanced";

    /// <summary>The line under the graph naming the selected step and what it does.</summary>
    /// <param name="title">The step's title.</param>
    /// <param name="summary">What the API says it does.</param>
    /// <param name="problem">What is not set yet, or null.</param>
    /// <returns>The words.</returns>
    internal static string WfStepLine(string title, string summary, string? problem) => problem is { Length: > 0 } ? $"{title}: {summary} ({problem})" : $"{title}: {summary}";

    /// <summary>A form field's name; a required one says so in words, not only by a mark.</summary>
    /// <param name="name">The config key.</param>
    /// <param name="required">True when the field must have a value.</param>
    /// <returns>The words.</returns>
    internal static string WfFieldCaption(string name, bool required) => required ? name + " (required)" : name;
}

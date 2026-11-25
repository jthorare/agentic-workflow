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

    /// <summary>The Done button that leaves graph edit mode.</summary>
    internal const string WfEditDone = "Done";

    /// <summary>The button that opens the selected step's settings.</summary>
    internal const string WfStepSettings = "Step settings";

    /// <summary>The question Done asks when a field of the step sheet changed.</summary>
    internal const string WfAskSave = "Save your changes?";

    /// <summary>The question's Discard button.</summary>
    internal const string WfAskDiscard = "Discard";

    /// <summary>The question's Keep editing button.</summary>
    internal const string WfAskKeep = "Keep editing";

    /// <summary>The editor sheet's Close button.</summary>
    internal const string WfEditClose = "Close";

    /// <summary>The editor sheet's line when no node is selected, or the selected node has no settings.</summary>
    internal const string WfEditorNoFields = "Select a step on the graph that has settings, then press Step settings.";

    /// <summary>The graph edit bar's line while the editor sheet is open: graph edits wait for the sheet to close.</summary>
    internal const string WfEditSheetOpenNote = "Close step settings to change the graph.";

    /// <summary>The editor sheet's heading when no step is selected.</summary>
    /// <param name="name">The workflow's name.</param>
    /// <returns>The words.</returns>
    internal static string WfEditorHeading(string name) => "Edit " + name;

    /// <summary>The editor sheet's heading for the selected step: its title and what kind of step it is.</summary>
    /// <param name="title">The step's own title, or its block's title when it has none.</param>
    /// <param name="kind"><see cref="WfEditorTriggerKind"/>, or the block's title.</param>
    /// <returns>The words.</returns>
    internal static string WfEditorStep(string title, string kind) => title + " \u00b7 " + kind;

    /// <summary>The selected step's id, shown only in the sheet's Advanced group (#3684): the card and the heading never show it.</summary>
    /// <param name="id">The node's id.</param>
    /// <returns>The line.</returns>
    internal static string WfEditorStepId(string id) => "Step id: " + id;

    /// <summary>The editor sheet's heading for a step that does not exist yet.</summary>
    /// <param name="kind">The block's title.</param>
    /// <returns>The words.</returns>
    internal static string WfEditorNewStep(string kind) => $"New step: {kind}";

    /// <summary>The line shown in place of the form when a new step has no settings.</summary>
    internal const string WfEditorNewStepNoFields = "This step has no settings. Save adds it.";

    /// <summary>The kind word in the editor heading of a trigger step.</summary>
    internal const string WfEditorTriggerKind = "trigger";

    /// <summary>The Advanced toggle under a field whose presets hide its raw text.</summary>
    internal const string WfFieldAdvanced = "Advanced";

    /// <summary>The line under the graph naming the selected step and what it does.</summary>
    /// <param name="title">The step's title.</param>
    /// <param name="summary">What the API says it does.</param>
    /// <param name="problem">What is not set yet, or null.</param>
    /// <returns>The words.</returns>
    internal static string WfStepLine(string title, string summary, string? problem) => problem is { Length: > 0 } ? $"{title} · {summary} ({problem})" : $"{title} · {summary}";

    /// <summary>A form field's name; a required one says so in words, not only by a mark.</summary>
    /// <param name="name">The config key.</param>
    /// <param name="required">True when the field must have a value.</param>
    /// <returns>The words.</returns>
    internal static string WfFieldCaption(string name, bool required) => required ? name + " (required)" : name;

    /// <summary>The line under a new step's first empty required field: why Save is off (#4330).</summary>
    /// <param name="name">The field's caption, without "(required)".</param>
    /// <returns>The words.</returns>
    internal static string WfFieldNeededToSave(string name) => $"Fill in {name} to save.";

    /// <summary>The line under a step's field when two or more fields of one "exactly one of" group have text (#4359).</summary>
    /// <param name="names">The group's captions, joined with "and" ("Wait for and Wait until").</param>
    /// <returns>The words.</returns>
    internal static string WfFieldOnlyOne(string names) => $"Fill in only one of {names}.";

    /// <summary>What the toast says after Save step; its Undo button follows it.</summary>
    internal const string WfStepSavedToast = "Step saved.";

    /// <summary>Why Undo did nothing: an edit or an Activate is on the way, the workflow changed elsewhere, or another workflow is open.</summary>
    internal const string WfUndoNotNow = "Not while a change is on its way, or after the workflow changed elsewhere. Reload it first.";
}

using System.Globalization;

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

    /// <summary>The editor sheet's heading.</summary>
    /// <param name="name">The workflow's name.</param>
    /// <param name="revision">The revision the edits are sent on.</param>
    /// <returns>The words.</returns>
    internal static string WfEditorHeading(string name, int revision) => "Edit " + name + " \u00b7 revision " + revision.ToString(CultureInfo.InvariantCulture);

    /// <summary>The editor sheet's line naming the node whose settings are shown.</summary>
    /// <param name="node">The node's id.</param>
    /// <returns>The words.</returns>
    internal static string WfEditorNode(string node) => $"Settings of {node}";

    /// <summary>A form field's name; a required one says so in words, not only by a mark.</summary>
    /// <param name="name">The config key.</param>
    /// <param name="required">True when the field must have a value.</param>
    /// <returns>The words.</returns>
    internal static string WfFieldCaption(string name, bool required) => required ? name + " (required)" : name;
}

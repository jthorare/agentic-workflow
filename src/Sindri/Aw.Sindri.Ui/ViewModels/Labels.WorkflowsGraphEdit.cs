namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the graph edits of the Workflows page, Add step, Remove and Connect (#2258 PR 17 part 3b). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The accessible name of the row of graph edit commands.</summary>
    internal const string WfEditBarName = "Edit the steps of this workflow";

    /// <summary>The command that adds a step.</summary>
    internal const string WfEditAdd = "Add step";

    /// <summary>The command that removes the selected step.</summary>
    internal const string WfEditRemove = "Remove";

    /// <summary>The command that connects the selected step to another.</summary>
    internal const string WfEditConnect = "Connect to…";

    /// <summary>The block picker's heading.</summary>
    internal const string WfEditPickBlock = "Pick the kind of step to add";

    /// <summary>The target list's heading.</summary>
    internal const string WfEditPickTarget = "Connect the selected step to:";

    /// <summary>The remove confirm's yes.</summary>
    internal const string WfEditConfirmYes = "Remove step";

    /// <summary>The close action of the picker, the target list and the confirm.</summary>
    internal const string WfEditCancel = "Cancel";

    /// <summary>The question before a step is removed.</summary>
    /// <param name="title">The step's title.</param>
    /// <returns>The words.</returns>
    internal static string WfEditConfirmRemove(string title) => $"Remove the step \"{title}\" and the connections it has?";

    /// <summary>One line of the target list.</summary>
    /// <param name="title">The step's title.</param>
    /// <param name="id">The step's id.</param>
    /// <returns>The words.</returns>
    internal static string WfEditTarget(string title, string id) => string.Equals(title, id, StringComparison.Ordinal) ? id : $"{title} ({id})";
}

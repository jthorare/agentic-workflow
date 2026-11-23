using System.Globalization;

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
    /// <param name="title">The step's card line 1.</param>
    /// <param name="line">The step's card line 2, added when two steps share line 1.</param>
    /// <returns>The words.</returns>
    internal static string WfEditTarget(string title, string line) => $"{title} \u00b7 {line}";

    /// <summary>A target line that still reads like another, with its place among them.</summary>
    /// <param name="text">The line.</param>
    /// <param name="n">Its place, from 1.</param>
    /// <returns>The words, "Code (2)".</returns>
    internal static string WfEditTargetNth(string text, int n) => text + " (" + n.ToString(CultureInfo.InvariantCulture) + ")";
}

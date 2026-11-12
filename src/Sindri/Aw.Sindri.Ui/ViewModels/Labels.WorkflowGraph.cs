using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the read-only workflow graph (#2258 PR 17 part 2). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The graph's accessible name.</summary>
    internal const string WfGraphName = "Workflow graph";

    /// <summary>The graph when the workflow has no steps.</summary>
    internal const string WfGraphEmpty = "This workflow has no steps.";

    /// <summary>A step's accessible name: title, kind, place in reading order and, when a run is shown, its status word.</summary>
    /// <param name="title">The step's title.</param>
    /// <param name="kind">The step's kind, one name per kind.</param>
    /// <param name="position">The step's 1-based place.</param>
    /// <param name="count">How many steps the graph draws.</param>
    /// <param name="word">The status word in lower case, or empty.</param>
    /// <returns>The words.</returns>
    internal static string WfGraphNodeName(string title, string kind, int position, int count, string word) =>
        string.Create(CultureInfo.InvariantCulture, $"{title}, {kind} step, {position} of {count}") + (word.Length == 0 ? string.Empty : ", " + word);

    /// <summary>The note over a graph whose nodes lack positions: "2 nodes have no position: a, b".</summary>
    /// <param name="ids">The ids of the nodes not drawn, in spec order.</param>
    /// <returns>The words.</returns>
    internal static string WfGraphUnplaced(IReadOnlyList<string> ids) =>
        string.Create(global::System.Globalization.CultureInfo.InvariantCulture, $"{ids.Count} {(ids.Count == 1 ? "node has" : "nodes have")} no position: {string.Join(", ", ids)}");
}

using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The graph edits (<see cref="GraphEditViewModel"/>, continued): the id of a new step.</summary>
internal sealed partial class GraphEditViewModel
{
    // the block type and the first number no node has: "run_agent_1", "run_agent_2", ...
    private string FreshId(string type)
    {
        var used = new HashSet<string>(Editor.Nodes.Select(n => n.Id), StringComparer.Ordinal);
        if (_runs.Graph is { } graph)
        {
            used.UnionWith(graph.Nodes.Select(n => n.Id));
            used.UnionWith(graph.Unplaced);
        }

        var number = 1;
        while (used.Contains(Candidate(type, number)))
        {
            number++;
        }

        return Candidate(type, number);
    }

    private static string Candidate(string type, int number) => type + "_" + number.ToString(CultureInfo.InvariantCulture);
}

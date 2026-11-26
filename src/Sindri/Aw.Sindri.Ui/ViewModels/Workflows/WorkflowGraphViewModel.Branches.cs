using System.Text.Json;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The spec's edge branches (<see cref="WorkflowGraphViewModel"/>, continued).</summary>
internal sealed partial class WorkflowGraphViewModel
{
    // Every (from, output) the spec has an edge for, whether or not its target has a served position.
    private static HashSet<(string From, string Branch)> SpecBranches(JsonElement spec)
    {
        HashSet<(string, string)> branches = [];
        if (spec.ValueKind == JsonValueKind.Object && spec.TryGetProperty("edges", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var edge in list.EnumerateArray())
            {
                _ = branches.Add((Text(edge, "from"), Text(edge, "output")));
            }
        }

        return branches;
    }
}

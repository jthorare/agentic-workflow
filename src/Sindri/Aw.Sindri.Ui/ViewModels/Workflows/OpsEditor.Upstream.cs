using System.Text.Json;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The workflow editor's logic (<see cref="OpsEditor"/>, continued): which steps a new step may read, the graph validator's "upstream" (GRAPH-REF-001).</summary>
internal sealed partial class OpsEditor
{
    // The edges of the workflow as read, as from and to ids.
    private IReadOnlyList<(string From, string To)> _edges = [];

    /// <summary>The steps a new step placed after <paramref name="after"/> may read: that step and every step with a path to it. None when the new step follows no step.</summary>
    /// <param name="after">The step the new one follows, or null for none.</param>
    /// <returns>The ids of those steps.</returns>
    internal IReadOnlySet<string> Upstream(string? after)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        var next = new Stack<string>();
        if (after is not null)
        {
            next.Push(after);
        }

        while (next.TryPop(out var id))
        {
            if (!found.Add(id))
            {
                continue;
            }

            foreach (var (from, _) in _edges.Where(e => string.Equals(e.To, id, StringComparison.Ordinal)))
            {
                next.Push(from);
            }
        }

        return found;
    }

    private static List<(string From, string To)> ReadEdges(JsonElement spec)
    {
        var edges = new List<(string From, string To)>();
        if (spec.ValueKind != JsonValueKind.Object || !spec.TryGetProperty("edges", out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return edges;
        }

        foreach (var edge in list.EnumerateArray())
        {
            if (Id(edge, "from") is { } from && Id(edge, "to") is { } to)
            {
                edges.Add((from, to));
            }
        }

        return edges;
    }

    private static string? Id(JsonElement edge, string name) =>
        edge.ValueKind == JsonValueKind.Object && edge.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

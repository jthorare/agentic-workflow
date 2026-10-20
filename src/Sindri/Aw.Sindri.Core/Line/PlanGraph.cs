using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Line;

/// <summary>graph.json, derived by code from tasks.json (line spec §6.2, §8.3): each phase's stack, wave and base rule, the waves at the profile's parallel, the critical path and the PR count.</summary>
public static class PlanGraph
{
    /// <summary>Derives graph.json.</summary>
    /// <param name="tasksJson">The text of tasks.json.</param>
    /// <param name="options">The profile values.</param>
    /// <returns>The graph.json text (indented, ending with a newline); null when tasks.json is no task plan.</returns>
    public static string? Derive(string tasksJson, PlanGraphOptions options)
    {
        ArgumentNullException.ThrowIfNull(tasksJson);
        ArgumentNullException.ThrowIfNull(options);
        var deps = Dependencies(tasksJson);
        if (deps is null)
        {
            return null;
        }

        var levels = deps.Keys.ToDictionary(static id => id, id => Level(id, deps, []), StringComparer.Ordinal);
        var order = deps.Keys.Select((id, index) => (Id: id, Index: index)).OrderBy(p => levels[p.Id]).ThenBy(static p => p.Index).Select(static p => p.Id).ToList();
        var stacks = Stacks(order, deps);
        var rows = Rows(order, levels, Math.Max(1, options.Parallel));
        var graph = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["parallel"] = options.Parallel,
            ["maxStack"] = options.MaxStack,
            ["phases"] = Phases(deps, stacks, rows, options),
            ["waves"] = Waves(rows),
            ["criticalPath"] = LineJson.StringArray(CriticalPath(deps, levels)),
            ["prCount"] = deps.Count,
        };
        return LineJson.Write(graph);
    }

    // The phases in task order with their dependencies that exist; null when tasks.json is not {tasks:[{id,deps}]}.
    private static Dictionary<string, List<string>>? Dependencies(string tasksJson)
    {
        try
        {
            if (JsonNode.Parse(tasksJson) is not JsonObject { } plan || plan["tasks"] is not JsonArray tasks)
            {
                return null;
            }

            var deps = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var task in tasks)
            {
                if (LineJson.Str(task, "id") is not { Length: > 0 } id || !deps.TryAdd(id, [.. LineJson.Strings(task, "deps")]))
                {
                    return null;
                }
            }

            foreach (var own in deps.Values)
            {
                _ = own.RemoveAll(d => !deps.ContainsKey(d));
            }

            return deps.Count > 0 ? deps : null;
        }
        catch (JsonException)
        {
            return null;   // boundary: tasks.json is text a person or a Step wrote
        }
    }

    // 1 plus the deepest dependency; a loop counts the phase it comes back to as level 0, so the walk ends.
    private static int Level(string id, Dictionary<string, List<string>> deps, HashSet<string> path)
    {
        if (!path.Add(id))
        {
            return 0;
        }

        var deepest = deps[id].Select(d => Level(d, deps, path)).DefaultIfEmpty(0).Max();
        _ = path.Remove(id);
        return 1 + deepest;
    }

    // A phase continues its base dependency's stack when no earlier sibling already did; otherwise (and for a phase with no dependency) it starts the next stack.
    private static Dictionary<string, int> Stacks(List<string> order, Dictionary<string, List<string>> deps)
    {
        var stacks = new Dictionary<string, int>(StringComparer.Ordinal);
        var continued = new HashSet<string>(StringComparer.Ordinal);
        var next = 1;
        foreach (var id in order)
        {
            var parent = deps[id].Find(stacks.ContainsKey);
            stacks[id] = parent is not null && continued.Add(parent) ? stacks[parent] : next++;
        }

        return stacks;
    }

    // Phases of one level in task order, split into rows of at most `parallel`.
    private static Dictionary<string, int> Rows(List<string> order, Dictionary<string, int> levels, int parallel)
    {
        var rows = new Dictionary<string, int>(StringComparer.Ordinal);
        var row = 0;
        foreach (var level in order.GroupBy(id => levels[id]))
        {
            foreach (var chunk in level.Chunk(parallel))
            {
                row++;
                foreach (var id in chunk)
                {
                    rows[id] = row;
                }
            }
        }

        return rows;
    }

    private static JsonArray Phases(Dictionary<string, List<string>> deps, Dictionary<string, int> stacks, Dictionary<string, int> rows, PlanGraphOptions options)
    {
        var phases = new JsonArray();
        foreach (var (id, own) in deps)
        {
            LineJson.Append(phases, new JsonObject
            {
                ["id"] = id,
                ["stack"] = stacks[id],
                ["wave"] = rows[id],
                ["base"] = own.Count == 0
                    ? new JsonObject { ["rule"] = "default-branch", ["ref"] = options.DefaultBranch }
                    : new JsonObject { ["rule"] = "dependency-head", ["ref"] = own[0] },
                ["waitsForMerge"] = own.Count > 1,
            });
        }

        return phases;
    }

    private static JsonArray Waves(Dictionary<string, int> rows)
    {
        var waves = new JsonArray();
        foreach (var row in rows.GroupBy(static p => p.Value).OrderBy(static g => g.Key))
        {
            LineJson.Append(waves, LineJson.StringArray(row.Select(static p => p.Key)));
        }

        return waves;
    }

    // The longest chain of dependencies: from the deepest phase back through its deepest dependency (the first in the list on a tie).
    private static List<string> CriticalPath(Dictionary<string, List<string>> deps, Dictionary<string, int> levels)
    {
        var path = new List<string>();
        var current = deps.Keys.First(id => levels[id] == levels.Values.Max());
        while (path.Count <= deps.Count)
        {
            path.Add(current);
            var below = deps[current].Where(d => levels[d] == levels[current] - 1).ToList();
            if (below.Count == 0)
            {
                break;
            }

            current = below[0];
        }

        path.Reverse();
        return path;
    }
}

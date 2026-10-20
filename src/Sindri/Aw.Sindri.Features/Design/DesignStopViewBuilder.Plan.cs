using System.Text.Json.Nodes;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Design;

internal static partial class DesignStopViewBuilder
{
    private static List<JsonNode> Phases(LineFacts files) => [.. LineJson.Items(files.Json("tasks.json"), "tasks")];

    // graph.json is derived by code from the tasks, with the profile's parallel and maxStack from 'options.json' when it is there.
    private static StopGraph Graph(LineFacts files, JsonNode? options)
    {
        var graphOptions = new PlanGraphOptions(LineJson.Number(options, "parallel") ?? 2, LineJson.Number(options, "maxStack") ?? 4, LineJson.Str(options, "defaultBranch") ?? "main");
        var derived = files.Has("tasks.json") && PlanGraph.Derive(files.Text("tasks.json"), graphOptions) is { } text ? JsonNode.Parse(text) : null;
        if (derived is null)
        {
            return new StopGraph(graphOptions.Parallel, graphOptions.MaxStack, 0, [], [], []);
        }

        var tasks = Phases(files).ToDictionary(static t => Text(t, "id"), static t => t, StringComparer.Ordinal);
        var hidden = HiddenCounts(files);
        var phases = LineJson.Items(derived, "phases").Select(g => Phase(g, tasks[Text(g, "id")], hidden)).ToList();
        var waves = LineJson.Items(derived, "waves").Select(static w => (IReadOnlyList<string>)[.. LineJson.Strings(w)]).ToList();
        return new StopGraph(graphOptions.Parallel, graphOptions.MaxStack, LineJson.Number(derived, "prCount") ?? 0, [.. LineJson.Strings(derived, "criticalPath")], waves, phases);
    }

    private static StopPhase Phase(JsonNode graph, JsonNode task, Dictionary<string, int> hidden)
    {
        var id = Text(graph, "id");
        var rules = LineJson.Strings(task, "rules").ToList();
        var kind = rules.Select(static r => r.StartsWith("kind:", StringComparison.Ordinal) ? r[5..] : null).OfType<string>().FirstOrDefault() ?? string.Empty;
        var baseRule = LineJson.Obj(graph, "base");
        return new StopPhase(
            id,
            Text(task, "title"),
            Text(task, "size"),
            kind,
            LineJson.Number(graph, "stack") ?? 0,
            LineJson.Number(graph, "wave") ?? 0,
            $"{Text(baseRule, "rule")} {Text(baseRule, "ref")}",
            LineJson.Bool(graph, "waitsForMerge") == true,
            [.. OwnItems(task).Select(static i => Text(i, "id"))],
            [.. LineJson.Strings(task, "writeScope")],
            [.. rules.Where(static r => r.StartsWith("test-change:", StringComparison.Ordinal)).Select(TestChange)],
            hidden.GetValueOrDefault(id));
    }

    // 'test-change:<path>:<reason>': the path ends at the first colon after the prefix.
    private static StopTestChange TestChange(string rule)
    {
        var rest = rule["test-change:".Length..];
        var colon = rest.IndexOf(':', StringComparison.Ordinal);
        return colon < 0 ? new StopTestChange(rest, string.Empty) : new StopTestChange(rest[..colon], rest[(colon + 1)..]);
    }

    private static IEnumerable<JsonNode> OwnItems(JsonNode phase) => LineJson.Items(phase, "acceptance").Where(static a => LineJson.Bool(a, "holdout") != true);

    // Hidden scenarios by count only: the sealed sets' counts less their canaries, or, before the sets are planned, the hidden entries the tasks carry.
    private static Dictionary<string, int> HiddenCounts(LineFacts files)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var set in LineJson.Items(files.Json("holdout.json"), "phases"))
        {
            counts[Text(set, "id")] = (LineJson.Number(set, "count") ?? 0) - (LineJson.Number(set, "canaries") ?? 0);
        }

        foreach (var task in Phases(files).Where(t => !counts.ContainsKey(Text(t, "id"))))
        {
            counts[Text(task, "id")] = LineJson.Items(task, "acceptance").Count(static a => LineJson.Bool(a, "holdout") == true);
        }

        return counts;
    }

    // Phase by phase, each item with the signals the design gave it.
    private static List<StopTraceRow> Trace(LineFacts files, LineFacts design, List<JsonNode> tasks)
    {
        var trace = new List<StopTraceRow>();
        if (!files.Has("tasks.json") || !design.Has("signals.json"))
        {
            return trace;
        }

        var signals = LineJson.Obj(design.Json("signals.json"), "signals");
        foreach (var task in tasks)
        {
            foreach (var item in OwnItems(task))
            {
                var id = Text(item, "id");
                trace.Add(new StopTraceRow(id, Text(task, "id"), [.. LineJson.Items(signals, id).Select(static s => new StopSignal(Text(s, "id"), Text(s, "kind"), Text(s, "runsAt"), Text(s, "statement")))]));
            }
        }

        return trace;
    }
}

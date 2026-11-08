using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Graph.Validation;

public sealed partial class GraphValidator
{
    private const int MaxCrons = 6;

    private void Node(GraphNode node, List<GraphFinding> found)
    {
        var id = node.Id.Value;
        if (!context.Blocks.TryGetValue(node.Type, out var block))
        {
            found.Add(Found.At("GRAPH-BLOCK-001", id, "type", $"there is no block type '{node.Type}'", $"use one of: {string.Join(", ", context.Blocks.Keys.Order(StringComparer.Ordinal))}"));
            return;
        }

        foreach (var (field, problem) in block.Config.Check(node.Config))
        {
            found.Add(Found.At("GRAPH-CONFIG-001", id, "config." + field, $"'{field}' {problem}", $"fix it as the {node.Type} block's example shows: {block.Example}"));
        }

        if (string.Equals(node.Type, "schedule", StringComparison.Ordinal))
        {
            Crons(node, found);
        }

        Names(node, found);
    }

    private void Crons(GraphNode node, List<GraphFinding> found)
    {
        var crons = StringsOf(node.Config["cron"]).ToList();
        var timezone = StringsOf(node.Config["timezone"]).FirstOrDefault();
        if (crons.Count > MaxCrons)
        {
            found.Add(Found.At("GRAPH-CRON-001", node.Id.Value, "config.cron", $"a schedule takes up to {MaxCrons} crons, this has {crons.Count}", "split it into two workflows or merge the crons"));
        }

        foreach (var problem in crons.Select(cron => context.CronProblem(cron, timezone)).OfType<string>())
        {
            found.Add(Found.At("GRAPH-CRON-001", node.Id.Value, "config.cron", problem, "write five fields (minute hour day month weekday) and a timezone name such as America/Los_Angeles"));
        }
    }

    private void Names(GraphNode node, List<GraphFinding> found)
    {
        if (string.Equals(node.Type, "run_agent", StringComparison.Ordinal))
        {
            Lookup(node, new NameCheck("repo", context.World.Repo, "GRAPH-REPO", "repo"), found);
            Lookup(node, new NameCheck("readOnlyRepos", context.World.Repo, "GRAPH-REPO", "repo"), found);
        }

        Lookup(node, new NameCheck("connection", context.World.Connection, "GRAPH-NAME", "connection"), found);
        Lookup(node, new NameCheck("memory", context.World.Memory, "GRAPH-NAME", "memory"), found);
    }

    private static void Lookup(GraphNode node, NameCheck check, List<GraphFinding> found)
    {
        // why: a name that is itself a reference can't be checked before the run
        foreach (var name in StringsOf(node.Config[check.Field]).Where(n => !n.Contains("{{", StringComparison.Ordinal)))
        {
            var answer = check.Ask(name);
            if (answer != GraphLookup.Known)
            {
                found.Add(Unanswered(node.Id.Value, check, name, answer));
            }
        }
    }

    private static GraphFinding Unanswered(string node, NameCheck check, string name, GraphLookup answer) => answer == GraphLookup.Unknown
        ? Found.At(check.Code + "-001", node, "config." + check.Field, $"there is no {check.What} '{name}'", $"use a {check.What} that exists, or add '{name}' first")
        : new GraphFinding(check.Code + "-002", GraphSeverity.Warning, [node], "config." + check.Field, $"could not check {check.What} '{name}' yet", $"check by hand that {check.What} '{name}' exists");

    private static IEnumerable<string> StringsOf(JsonNode? node) => node switch
    {
        JsonArray list => list.OfType<JsonValue>().Select(static v => v.TryGetValue<string>(out var s) ? s : null).OfType<string>(),
        JsonValue value when value.TryGetValue<string>(out var text) => [text],
        _ => [],
    };

    private readonly record struct NameCheck(string Field, Func<string, GraphLookup> Ask, string Code, string What);
}

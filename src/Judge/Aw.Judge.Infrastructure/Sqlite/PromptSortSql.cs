using System.Text.Json.Nodes;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.PromptSort;
using Aw.Kernel.Data;

namespace Aw.Judge.Infrastructure.Sqlite;

/// <summary>The prompt sorter store's SQL: named constants and the statements that bind them.</summary>
internal static class PromptSortSql
{
    // A second save of a decision replaces its first, so the axes are cleared before they are written.
    internal const string DeleteAxes = "DELETE FROM prompt_sort_axes WHERE decision_id = $decision_id";

    internal const string InsertAxis =
        "INSERT INTO prompt_sort_axes (decision_id, axis, value, heuristic, source, status, probability, confidence) " +
        "VALUES ($decision_id, $axis, $value, $heuristic, $source, $status, $probability, $confidence)";

    internal const string UpsertRun =
        "INSERT OR REPLACE INTO prompt_sort_runs (decision_id, mode, reason, would_fire, fired, suppressed) " +
        "VALUES ($decision_id, $mode, $reason, $would_fire, $fired, $suppressed)";

    internal const string FindAxes =
        "SELECT axis, value, heuristic, source, status, probability, confidence FROM prompt_sort_axes WHERE decision_id = $decision_id ORDER BY axis";

    internal const string FindRun =
        "SELECT mode, reason, would_fire, fired, suppressed FROM prompt_sort_runs WHERE decision_id = $decision_id";

    internal static SqlStatement ClearAxes(DecisionId id) =>
        new(DeleteAxes, c => c.Bind("$decision_id", id.Value), "prompt sort axes " + id.Value);

    internal static SqlStatement Axis(DecisionId id, SortedAxis a) => new(InsertAxis, c => c
        .Bind("$decision_id", id.Value).Bind("$axis", a.Axis).Bind("$value", a.Value).Bind("$heuristic", a.Heuristic)
        .Bind("$source", a.Source).Bind("$status", a.Status).Bind("$probability", a.Probability).Bind("$confidence", a.Confidence),
        "prompt sort axis " + a.Axis);

    internal static SqlStatement Run(DecisionId id, SortRun run) => new(UpsertRun, c => c
        .Bind("$decision_id", id.Value).Bind("$mode", run.Mode).Bind("$reason", run.Reason)
        .Bind("$would_fire", Names(run.WouldFire)).Bind("$fired", Names(run.Fired)).Bind("$suppressed", Objects(run.Suppressed)),
        "prompt sort run " + id.Value);

    internal static SqlStatement Axes(DecisionId id) =>
        new(FindAxes, c => c.Bind("$decision_id", id.Value), "prompt sort axes " + id.Value);

    internal static SqlStatement FindRunOf(DecisionId id) =>
        new(FindRun, c => c.Bind("$decision_id", id.Value), "prompt sort run " + id.Value);

    // JSON array of strings, as the TS sorter stored it.
    private static string Names(IReadOnlyList<string> names) => new JsonArray([.. names.Select(static n => (JsonNode?)JsonValue.Create(n))]).ToJsonString();

    // JSON array of objects: each suppression is already an object's text.
    private static string Objects(IReadOnlyList<string> objects) => new JsonArray([.. objects.Select(static o => JsonNode.Parse(o))]).ToJsonString();
}

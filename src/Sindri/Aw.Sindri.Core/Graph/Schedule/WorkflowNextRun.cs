using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Graph.Schedule;

/// <summary>
/// When a workflow next runs on its own: the soonest tick of its trigger's schedule. The list row (#3683) and GET /sindri/workflows/{slug} (#3756) both read it here, so the two always say the same time.
/// </summary>
public static class WorkflowNextRun
{
    /// <summary>The trigger node of a graph: the first node nothing leads into (a graph has exactly one trigger), or the first node.</summary>
    /// <param name="spec">The graph.</param>
    /// <returns>The node, or null for an empty graph.</returns>
    public static GraphNode? Trigger(GraphSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return spec.Nodes.FirstOrDefault(n => spec.Edges.All(e => e.To != n.Id)) ?? (spec.Nodes.Count > 0 ? spec.Nodes[0] : null);
    }

    /// <summary>The cron lines of a schedule node: its 'cron' config as one string or a list of strings; anything else reads as none.</summary>
    /// <param name="node">The node.</param>
    /// <returns>The cron lines.</returns>
    public static IReadOnlyList<string> Crons(GraphNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return node.Config["cron"] switch
        {
            JsonValue v when v.TryGetValue<string>(out var text) => [text],
            JsonArray list => [.. list.OfType<JsonValue>().Select(static v => v.TryGetValue<string>(out var s) ? s : null).OfType<string>()],
            _ => [],
        };
    }

    /// <summary>The next run of <paramref name="record"/> after <paramref name="now"/>.</summary>
    /// <param name="record">The workflow; only an active one (ActiveRevision set) runs on a schedule, so a paused, archived or never-activated one has none.</param>
    /// <param name="spec">The graph of the revision it runs.</param>
    /// <param name="now">The instant to start after.</param>
    /// <returns>The tick, or null when it is off, its trigger is no schedule, or the schedule has no valid cron or no tick.</returns>
    public static DateTimeOffset? Of(WorkflowRecord record, GraphSpec spec, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.ActiveRevision is null || Trigger(spec) is not { } node || !string.Equals(node.Type, "schedule", StringComparison.Ordinal) || !TryZone(node, out var timezone))
        {
            return null;
        }

        try
        {
            return Soonest(Crons(node), timezone, now);
        }
        catch (InvalidOperationException)
        {
            // why: a cron with no tick in nine years (30 February) throws from the walk; one workflow's odd schedule must not 500 its read or the whole list, so it shows no next run
            return null;
        }
    }

    // why: validate does not check the type of a timezone, so a stored number or list reaches here; GetValue<string>() would throw, so that workflow shows no next run
    private static bool TryZone(GraphNode node, out string? timezone)
    {
        timezone = null;
        return node.Config["timezone"] is not { } zone || (zone is JsonValue value && value.TryGetValue(out timezone));
    }

    private static DateTimeOffset? Soonest(IReadOnlyList<string> crons, string? timezone, DateTimeOffset now)
    {
        DateTimeOffset? soonest = null;
        foreach (var text in crons)
        {
            if (CronExpression.TryParse(text, timezone, out var cron, out _) && cron.Next(now) is var tick && (soonest is null || tick < soonest))
            {
                soonest = tick;
            }
        }

        return soonest;
    }
}

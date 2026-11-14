using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Schedule;

namespace Aw.Sindri.Features.ListWorkflows;

/// <summary>A workflow's trigger in words for the list row (#3683), and the next tick of its schedule.</summary>
internal static class TriggerWords
{
    /// <summary>The trigger node of a graph: the first node nothing leads into (a graph has exactly one trigger), or the first node.</summary>
    /// <param name="spec">The graph.</param>
    /// <returns>The node, or null for an empty graph.</returns>
    internal static GraphNode? Trigger(GraphSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return spec.Nodes.FirstOrDefault(n => spec.Edges.All(e => e.To != n.Id)) ?? (spec.Nodes.Count > 0 ? spec.Nodes[0] : null);
    }

    /// <summary>The trigger in words: 'By hand', 'Every 20 min', 'Every weekday 7:00', 'Every day 7:00'; a schedule it has no words for shows its cron text, any other trigger its node type.</summary>
    /// <param name="node">The trigger node, or null.</param>
    /// <returns>The words.</returns>
    internal static string Of(GraphNode? node)
    {
        if (node is null)
        {
            return "";
        }

        if (string.Equals(node.Type, "manual", StringComparison.Ordinal))
        {
            return "By hand";
        }

        if (!string.Equals(node.Type, "schedule", StringComparison.Ordinal))
        {
            return node.Type;
        }

        var crons = Crons(node);
        return crons.Count switch
        {
            0 => "On a schedule",
            1 => Words(crons[0]),
            _ => string.Create(CultureInfo.InvariantCulture, $"{crons.Count} schedules"),
        };
    }

    /// <summary>The soonest tick of the node's cron lines after <paramref name="now"/>.</summary>
    /// <param name="node">The trigger node, or null.</param>
    /// <param name="now">The instant to start after.</param>
    /// <returns>The tick, or null when the node is no schedule or has no valid cron.</returns>
    internal static DateTimeOffset? Next(GraphNode? node, DateTimeOffset now)
    {
        if (node is null || !string.Equals(node.Type, "schedule", StringComparison.Ordinal))
        {
            return null;
        }

        var timezone = node.Config["timezone"]?.GetValue<string>();
        DateTimeOffset? soonest = null;
        foreach (var text in Crons(node))
        {
            if (CronExpression.TryParse(text, timezone, out var cron, out _) && cron.Next(now) is var tick && (soonest is null || tick < soonest))
            {
                soonest = tick;
            }
        }

        return soonest;
    }

    private static List<string> Crons(GraphNode node) => node.Config["cron"] switch
    {
        JsonValue v when v.TryGetValue<string>(out var text) => [text],
        JsonArray list => [.. list.OfType<JsonValue>().Select(static v => v.TryGetValue<string>(out var s) ? s : null).OfType<string>()],
        _ => [],
    };

    private static string Words(string cron)
    {
        var f = cron.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (f.Length != 5)
        {
            return cron;
        }

        return Interval(f) ?? Daily(f) ?? cron;
    }

    private static string? Interval(string[] f) =>
        f[0].StartsWith("*/", StringComparison.Ordinal) && int.TryParse(f[0][2..], NumberStyles.None, CultureInfo.InvariantCulture, out var every) && every > 0 && f[1] == "*" && f[2] == "*" && f[3] == "*" && f[4] == "*"
            ? string.Create(CultureInfo.InvariantCulture, $"Every {every} min")
            : null;

    private static string? Daily(string[] f)
    {
        if (!int.TryParse(f[0], NumberStyles.None, CultureInfo.InvariantCulture, out var minute) || !int.TryParse(f[1], NumberStyles.None, CultureInfo.InvariantCulture, out var hour) || f[2] != "*" || f[3] != "*")
        {
            return null;
        }

        var time = string.Create(CultureInfo.InvariantCulture, $"{hour}:{minute:D2}");
        return f[4] switch
        {
            "*" => "Every day " + time,
            "1-5" or "MON-FRI" => "Every weekday " + time,
            _ => null,
        };
    }
}

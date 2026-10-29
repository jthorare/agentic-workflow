using System.Globalization;
using System.Text.Json.Nodes;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>The variables of the adapter's operations: the filters (flat objects, whose keys Linear ANDs) and the ids and counts around them.</summary>
internal static class LinearFilters
{
    /// <summary>Records whose <paramref name="field"/> contains any of the terms (case-insensitive), changed since a time, in some projects or teams.</summary>
    /// <param name="field">'title', 'name' or 'body'.</param>
    /// <param name="terms">The terms; none leaves the title or body unconstrained.</param>
    /// <param name="since">Only records updated at or after this time.</param>
    public static JsonObject Matching(string field, IReadOnlyList<string> terms, DateTimeOffset? since)
    {
        var filter = new JsonObject();
        if (terms.Count > 0)
        {
            filter["or"] = new JsonArray([.. terms.Select(t => (JsonNode)new JsonObject { [field] = new JsonObject { ["containsIgnoreCase"] = t } })]);
        }

        if (since is { } at)
        {
            filter["updatedAt"] = new JsonObject { ["gte"] = at.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) };
        }

        return filter;
    }

    /// <summary>The filter limited to records of a project that one of these teams can see (Linear applies it; the adapter keeps no list of projects).</summary>
    public static JsonObject InProjectsOfTeams(JsonObject filter, IEnumerable<string> teams)
    {
        filter["project"] = new JsonObject { ["accessibleTeams"] = TeamsSome(teams) };
        return filter;
    }

    /// <summary>The project filter limited to projects that one of these teams can see.</summary>
    public static JsonObject OfTeams(JsonObject filter, IEnumerable<string> teams)
    {
        filter["accessibleTeams"] = TeamsSome(teams);
        return filter;
    }

    /// <summary>A filter for records of this project.</summary>
    public static JsonObject InProject(string projectId) => new() { ["project"] = new JsonObject { ["id"] = new JsonObject { ["eq"] = projectId } } };

    /// <summary>The filter limited to issues of these teams.</summary>
    public static JsonObject InTeams(JsonObject filter, IEnumerable<string> teams)
    {
        filter["team"] = new JsonObject { ["key"] = new JsonObject { ["in"] = Strings(teams) } };
        return filter;
    }

    /// <summary>The filter for a first read with no watermark: issues updated since the window's start, or open in a project that is not done or canceled however old they are (projects page spec §4.4).</summary>
    public static JsonObject UpdatedOrOpenInOpenProject(JsonObject filter, string windowStart)
    {
        filter["or"] = new JsonArray(
            new JsonObject { ["updatedAt"] = new JsonObject { ["gte"] = windowStart } },
            new JsonObject { ["state"] = ClosedTypes(), ["project"] = new JsonObject { ["status"] = ClosedTypes() } });
        return filter;
    }

    /// <summary>A filter for records with these ids.</summary>
    public static JsonObject WithIds(IEnumerable<string> ids) => new() { ["id"] = new JsonObject { ["in"] = Strings(ids) } };

    /// <summary>The variables of a filtered, counted read, from the cursor <paramref name="after"/> when there is one.</summary>
    public static JsonObject Variables(JsonObject? filter, int first, string? after = null)
    {
        var variables = new JsonObject { ["first"] = first };
        if (after is not null)
        {
            variables["after"] = after;
        }

        if (filter is not null)
        {
            variables["filter"] = filter;
        }

        return variables;
    }

    /// <summary>The variables of a read by id.</summary>
    public static JsonObject ById(string id) => new() { ["id"] = id };

    private static JsonObject ClosedTypes() => new() { ["type"] = new JsonObject { ["nin"] = Strings(["completed", "canceled"]) } };

    private static JsonObject TeamsSome(IEnumerable<string> teams) => new() { ["some"] = new JsonObject { ["key"] = new JsonObject { ["in"] = Strings(teams) } } };

    private static JsonArray Strings(IEnumerable<string> values) => new([.. values.Select(v => (JsonNode?)JsonValue.Create(v))]);
}

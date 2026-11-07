using System.Globalization;

namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>The paths of the Linear reads (Linear hub spec §3).</summary>
internal static class LinearRoutes
{
    internal const string Issues = "/sindri/linear/issues";

    /// <summary>GET: the list for <paramref name="query"/>, only the filters it sets, each escaped.</summary>
    internal static string IssueList(LinearIssueQuery query)
    {
        var parts = new List<string> { "group=" + Uri.EscapeDataString(query.Group) };
        Add(parts, "team", query.Team);
        Add(parts, "project", query.Project);
        Add(parts, "cycle", query.Cycle);
        Add(parts, "state", query.State);
        Add(parts, "assignee", query.Assignee);
        Add(parts, "priority", query.Priority?.ToString(CultureInfo.InvariantCulture));
        Add(parts, "label", query.Label);
        Add(parts, "after", query.After);
        Add(parts, "limit", query.Limit?.ToString(CultureInfo.InvariantCulture));
        return Issues + "?" + string.Join('&', parts);
    }

    /// <summary>GET: one issue by key (such as ENG-12).</summary>
    internal static string Issue(string key) => Issues + "/" + Uri.EscapeDataString(key);

    /// <summary>GET: the lookups of the team with key <paramref name="team"/>.</summary>
    internal static string Lookups(string team) => "/sindri/linear/lookups/" + Uri.EscapeDataString(team);

    /// <summary>GET: what changed since the marker <paramref name="after"/>, or since the first watermark when null.</summary>
    internal static string Since(string? after) => "/sindri/linear/since" + (after is null ? string.Empty : "?after=" + Uri.EscapeDataString(after));

    /// <summary>POST: an edit of one issue.</summary>
    internal static string Change(string key) => Issue(key) + "/change";

    /// <summary>POST: a comment on one issue.</summary>
    internal static string Comments(string key) => Issue(key) + "/comments";

    /// <summary>GET the connection, POST a key to save.</summary>
    internal const string Connection = "/sindri/linear/connection";

    /// <summary>POST: the teams a pasted read key can see.</summary>
    internal const string ConnectionTeams = Connection + "/teams";

    /// <summary>DELETE: the write key.</summary>
    internal const string ConnectionWrite = Connection + "/write";

    private static void Add(List<string> parts, string name, string? value)
    {
        if (value is not null)
        {
            parts.Add(name + "=" + Uri.EscapeDataString(value));
        }
    }
}

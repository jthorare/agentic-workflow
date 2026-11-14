namespace Aw.Sindri.Ui.Api.Sessions;

/// <summary>The paths of the session reads (collector spec CA-1, CA-2). Each path segment is escaped, so a slash in an id never becomes a segment.</summary>
internal static class SessionRoutes
{
    private const string Root = "/sindri/sessions";

    /// <summary>POST /sindri/sessions: start a one-off agent session.</summary>
    internal const string Start = Root;

    /// <summary>GET /sindri/sessions/last-start: the repo and agent of the last start.</summary>
    internal const string LastStart = Root + "/last-start";

    internal static string List(SessionQuery query)
    {
        var parts = new List<string>();
        Add(parts, "provider", query.Provider);
        Add(parts, "state", query.State);
        Add(parts, "repo", query.Repo);
        if (query.InWorkflow is { } inWorkflow)
        {
            parts.Add(inWorkflow ? "inWorkflow=true" : "inWorkflow=false");
        }

        return parts.Count == 0 ? Root : $"{Root}?{string.Join('&', parts)}";
    }

    internal static string One(string provider, string id) => $"{Root}/{Uri.EscapeDataString(provider)}/{Uri.EscapeDataString(id)}";

    private static void Add(List<string> parts, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            parts.Add($"{name}={Uri.EscapeDataString(value)}");
        }
    }
}

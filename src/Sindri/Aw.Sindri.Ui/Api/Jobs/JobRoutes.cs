namespace Aw.Sindri.Ui.Api.Jobs;

/// <summary>The paths of the job calls (API P6). Each path segment is escaped, so a slash in an id never becomes a segment.</summary>
internal static class JobRoutes
{
    private const string Root = "/sindri/jobs";

    internal static string List(JobQuery query)
    {
        var parts = new List<string>();
        Add(parts, "state", query.State);
        Add(parts, "kind", query.Kind);
        return parts.Count == 0 ? Root : $"{Root}?{string.Join('&', parts)}";
    }

    internal static string One(string id) => $"{Root}/{Uri.EscapeDataString(id)}";

    internal static string Cancel(string id) => $"{One(id)}/cancel";

    private static void Add(List<string> parts, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            parts.Add($"{name}={Uri.EscapeDataString(value)}");
        }
    }
}

using System.Text;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>
/// The paths of the workflow reads (API §3.8). Routes take the instance's opaque id and nothing else (Q-API-2): an
/// address is only ever a query value, escaped, so a slash in it never becomes a segment.
/// </summary>
internal static class WorkflowRoutes
{
    private const string Root = "/sindri/workflow";

    internal static string Instances(InstanceQuery query)
    {
        var parts = new List<string>();
        if (query.Root)
        {
            parts.Add("root=true");
        }

        if (query.Open)
        {
            parts.Add("open=true");
        }

        Add(parts, "recipe", query.Recipe);
        Add(parts, "subject", query.Subject);
        Add(parts, "address", query.Address);
        return parts.Count == 0 ? $"{Root}/instances" : $"{Root}/instances?{string.Join('&', parts)}";
    }

    internal static string Instance(string id) => $"{Root}/instances/{Uri.EscapeDataString(id)}";

    /// <summary>POST: the report for an open desk run.</summary>
    internal static string Complete(string id, string run) => $"{Instance(id)}/runs/{Uri.EscapeDataString(run)}/complete";

    /// <summary>POST: a command on the instance (an answer, a cancel, a hand back).</summary>
    internal static string Commands(string id) => $"{Instance(id)}/commands";

    internal static string Events(string id) => $"{Instance(id)}/events";

    internal static string Fact(string id, string key) => $"{Instance(id)}/facts/{Uri.EscapeDataString(key)}";

    /// <summary>GET: the recipe list (every registered recipe).</summary>
    internal static string Recipes => $"{Root}/recipes";

    /// <summary>POST: start an instance.</summary>
    internal static string Start => $"{Root}/instances";

    /// <summary>GET: the approve-all plan; POST: approve it (a job).</summary>
    internal static string ApproveAll => $"{Root}/recipes/approve-all";

    /// <summary>POST: a challenge the app answers by holding or typing.</summary>
    internal static string AppChallenge => $"{Root}/challenges/app";

    /// <summary>GET: one job, which the approval is followed through.</summary>
    internal static string Job(string id) => $"/sindri/jobs/{Uri.EscapeDataString(id)}";

    internal static string Recipe(string recipe, string? hash)
    {
        var path = new StringBuilder(Root).Append("/recipes/").Append(Uri.EscapeDataString(recipe));
        return (string.IsNullOrEmpty(hash) ? path : path.Append('@').Append(Uri.EscapeDataString(hash))).ToString();
    }

    /// <summary>POST: approve one recipe hash (a protected command; the body is the challenge and its typed text).</summary>
    internal static string ApproveRecipe(string recipe, string hash) => Recipe(recipe, hash) + "/approve";

    private static void Add(List<string> parts, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            parts.Add($"{name}={Uri.EscapeDataString(value)}");
        }
    }
}

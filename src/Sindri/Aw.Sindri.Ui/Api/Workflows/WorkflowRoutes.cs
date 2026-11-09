namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>The paths of the workflow reads (#2258, PR 8a). Each segment is escaped, so a run id's '@' and ':' never become path syntax.</summary>
internal static class WorkflowRoutes
{
    private const string Root = "/sindri/workflows";

    internal static string List() => Root;

    internal static string One(string slug) => $"{Root}/{Uri.EscapeDataString(slug)}";

    internal static string Runs(string slug) => $"{One(slug)}/runs";

    internal static string Run(string runId) => $"/sindri/workflow-runs/{Uri.EscapeDataString(runId)}";

    internal static string Cancel(string runId) => $"{Run(runId)}/cancel";

    internal static string Blocks() => $"{Root}/blocks";

    internal static string Ops(string slug) => $"{One(slug)}/ops";

    internal static string Activate(string slug) => $"{One(slug)}/activate";

    internal static string Pause(string slug) => $"{One(slug)}/pause";
}

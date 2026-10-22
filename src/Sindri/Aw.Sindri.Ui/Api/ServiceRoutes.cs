namespace Aw.Sindri.Ui.Api;

/// <summary>The paths of the service and stop commands (API P2). A service name is escaped, so a slash never becomes a segment.</summary>
internal static class ServiceRoutes
{
    /// <summary>POST: stops Sindri (StopProcess).</summary>
    internal const string Stop = "/sindri/stop";

    internal static string Run(string name) => $"/sindri/services/{Uri.EscapeDataString(name)}/run";

    internal static string Pause(string name) => $"/sindri/services/{Uri.EscapeDataString(name)}/pause";

    internal static string Resume(string name) => $"/sindri/services/{Uri.EscapeDataString(name)}/resume";
}

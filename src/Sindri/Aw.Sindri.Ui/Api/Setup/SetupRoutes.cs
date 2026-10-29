namespace Aw.Sindri.Ui.Api.Setup;

/// <summary>The paths of the setup calls (setup-once spec §7.2). The reads and the writes the app sends itself; the writes are desk actions, never a protected one.</summary>
internal static class SetupRoutes
{
    /// <summary>GET /sindri/setup.</summary>
    internal const string Read = "/sindri/setup";

    /// <summary>POST /sindri/setup/repos (AddRepo).</summary>
    internal const string Repos = "/sindri/setup/repos";

    /// <summary>POST /sindri/setup/mcp (RegisterMcp).</summary>
    internal const string Mcp = "/sindri/setup/mcp";
}

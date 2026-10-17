namespace Aw.Sindri.Core.Onyx;

/// <summary>The routes `sindri onyx up` asks the running Onyx to know when it is ready (v4.9.0, through nginx on the published port).</summary>
public static class OnyxRoutes
{
    /// <summary>The route answering 2xx once the API server is up. The /api prefix comes from nginx, not the backend: confirm on the running stack.</summary>
    public const string Health = "/api/health";

    /// <summary>The route that reports the release the backend runs, as JSON with `backend_version` (the field `OnyxClient` in Aw.Context reads). Public in v4.9.0, so no key is sent.</summary>
    public const string Version = "/api/version";
}

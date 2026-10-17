using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>What the Onyx on a loopback port does, for the readiness contract: whether anything listens, what the health route answers, what the version route sends, and the probe that must result.</summary>
/// <param name="Listening">Something listens on the port.</param>
/// <param name="HealthStatus">The health route's status.</param>
/// <param name="VersionStatus">The version route's status.</param>
/// <param name="VersionBody">The version route's body.</param>
/// <param name="Expected">The probe a correct implementation returns.</param>
public sealed record OnyxServerScript(bool Listening, int HealthStatus, int VersionStatus, string VersionBody, OnyxProbe Expected)
{
    /// <summary>Gets a port nothing listens on.</summary>
    public static OnyxServerScript Nothing { get; } = new(Listening: false, 0, 0, "", new OnyxProbe(Healthy: false, BackendVersion: null));

    /// <summary>Gets a server that is ready and reports a release.</summary>
    /// <param name="version">The release it reports.</param>
    /// <returns>The script.</returns>
    public static OnyxServerScript Ready(string version) => new(Listening: true, 200, 200, "{\"backend_version\":\"" + version + "\"}", new OnyxProbe(Healthy: true, version));

    /// <summary>Gets a server whose health route answers a server error.</summary>
    public static OnyxServerScript Failing { get; } = new(Listening: true, 503, 200, "{\"backend_version\":\"v4.9.0\"}", new OnyxProbe(Healthy: false, BackendVersion: null));

    /// <summary>Gets a server that is healthy and answers its version route with something that isn't the JSON it should be.</summary>
    public static OnyxServerScript GarbledVersion { get; } = new(Listening: true, 200, 200, "<html>not json</html>", new OnyxProbe(Healthy: true, BackendVersion: null));

    /// <summary>Gets a server that is healthy and has no version route.</summary>
    public static OnyxServerScript NoVersionRoute { get; } = new(Listening: true, 200, 404, "", new OnyxProbe(Healthy: true, BackendVersion: null));
}

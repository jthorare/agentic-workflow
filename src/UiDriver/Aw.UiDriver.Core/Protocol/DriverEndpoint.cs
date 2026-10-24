namespace Aw.UiDriver.Core.Protocol;

/// <summary>The driver.json file: where a running driver answers.</summary>
/// <param name="Schema">Always <see cref="SchemaName"/>.</param>
/// <param name="Url">The origin ('http://127.0.0.1:PORT').</param>
/// <param name="Pid">The app's process id.</param>
internal sealed record DriverEndpoint(string Schema, string Url, int Pid)
{
    /// <summary>The schema this build writes and reads.</summary>
    internal const string SchemaName = "aw-ui-driver/1";

    /// <summary>The endpoint file's name inside the driver directory.</summary>
    internal const string EndpointFile = "driver.json";

    /// <summary>The token file's name inside the driver directory (0600).</summary>
    internal const string TokenFile = "driver.token";

    /// <summary>The header that carries the token.</summary>
    internal const string TokenHeader = "X-Ui-Driver-Token";
}

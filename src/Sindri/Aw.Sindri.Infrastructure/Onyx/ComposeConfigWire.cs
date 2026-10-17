using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Onyx;

/// <summary>The part of `docker compose config --format json` Sindri reads: each service's published ports and the settings that give it the host (Compose's own snake_case wire format; a newer Compose adding a field still reads).</summary>
/// <param name="Services">The services by name.</param>
internal sealed record ComposeConfigWire([property: JsonPropertyName("services")] Dictionary<string, ComposeConfigWire.ServiceWire>? Services = null)
{
    /// <summary>One service.</summary>
    /// <param name="Ports">Its published ports.</param>
    /// <param name="NetworkMode">network_mode, such as host.</param>
    /// <param name="Privileged">privileged.</param>
    /// <param name="Pid">pid, such as host.</param>
    internal sealed record ServiceWire(
        [property: JsonPropertyName("ports")] List<PortWire>? Ports = null,
        [property: JsonPropertyName("network_mode")] string? NetworkMode = null,
        [property: JsonPropertyName("privileged")] bool? Privileged = null,
        [property: JsonPropertyName("pid")] string? Pid = null);

    /// <summary>One published port; `published` is a string in Compose's output (and may be absent).</summary>
    /// <param name="HostIp">The address it binds.</param>
    /// <param name="Target">The container port.</param>
    /// <param name="Published">The host port.</param>
    internal sealed record PortWire(
        [property: JsonPropertyName("host_ip")] string? HostIp = null,
        [property: JsonPropertyName("target")] int? Target = null,
        [property: JsonPropertyName("published"), JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] int? Published = null);
}

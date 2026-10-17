namespace Aw.Sindri.Core.Onyx;

/// <summary>One port a service of the rendered compose publishes on the host (from `docker compose config --format json`, before anything starts).</summary>
/// <param name="Service">The compose service name.</param>
/// <param name="HostIp">The host address it binds, empty when the compose doesn't name one (every interface).</param>
/// <param name="Published">The host port, 0 when the compose leaves it to Docker.</param>
/// <param name="Target">The port in the container.</param>
public sealed record ComposePublishedPort(string Service, string HostIp, int Published, int Target);

namespace Aw.Sindri.Core.Onyx;

/// <summary>A setting of a service in the rendered compose that gives its container the host's network, processes or privileges, and so reaches the host without publishing a port.</summary>
/// <param name="Service">The compose service name.</param>
/// <param name="Setting">The setting as written in the compose file, such as `network_mode: host`, `privileged: true` or `pid: host`.</param>
public sealed record ComposeHostAccess(string Service, string Setting);

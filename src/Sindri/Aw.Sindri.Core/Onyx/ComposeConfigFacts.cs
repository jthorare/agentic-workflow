namespace Aw.Sindri.Core.Onyx;

/// <summary>What Sindri reads from the rendered compose (`docker compose config --format json`) before it starts anything.</summary>
/// <param name="Ports">Every port a service publishes on the host.</param>
/// <param name="HostAccess">Every service setting that gives a container the host's network, processes or privileges.</param>
public sealed record ComposeConfigFacts(IReadOnlyList<ComposePublishedPort> Ports, IReadOnlyList<ComposeHostAccess> HostAccess);

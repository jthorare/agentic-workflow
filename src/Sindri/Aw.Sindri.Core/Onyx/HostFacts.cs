namespace Aw.Sindri.Core.Onyx;

/// <summary>What the host itself reports for the Onyx preflight: its disks and its loopback ports.</summary>
/// <param name="Home">Free disk at the Sindri home, where Onyx's files live.</param>
/// <param name="DataRoot">Free disk at Docker's data root when that is a directory on this host (a Linux engine); null when it is unknown or inside a VM.</param>
/// <param name="Port">Which loopback ports are free.</param>
public sealed record HostFacts(DiskFacts Home, DiskFacts? DataRoot, PortFacts Port);

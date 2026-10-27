namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The network rules of a plan (sandbox spec §3.2).</summary>
/// <param name="Mode">Enforce or learn.</param>
/// <param name="Groups">The allowed host groups.</param>
/// <param name="SindriPort">The Sindri port the inside forwarder listens on.</param>
/// <param name="ProxyPort">The egress proxy port the inside forwarder listens on.</param>
/// <param name="NetDir">The short per-launch socket directory on the host.</param>
public sealed record PlanNetwork(NetworkMode Mode, IReadOnlyList<PlanGroup> Groups, int SindriPort, int ProxyPort, string NetDir);

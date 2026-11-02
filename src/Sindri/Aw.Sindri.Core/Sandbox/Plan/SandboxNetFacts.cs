namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The network facts of one launch.</summary>
/// <param name="NetDir">The short per-launch socket directory on the host.</param>
/// <param name="SindriPort">The Sindri port.</param>
/// <param name="ProxyPort">The egress proxy port.</param>
public sealed record SandboxNetFacts(string NetDir, int SindriPort, int ProxyPort);

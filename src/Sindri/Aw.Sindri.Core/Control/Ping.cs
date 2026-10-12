namespace Aw.Sindri.Core.Control;

/// <summary>The control plane's answer to a liveness ping.</summary>
/// <param name="Pid">Its process id.</param>
/// <param name="Epoch">The lease epoch it holds (0 when it holds none).</param>
public sealed record Ping(int Pid, long Epoch);

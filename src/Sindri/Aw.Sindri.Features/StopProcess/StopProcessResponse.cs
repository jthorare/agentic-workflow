namespace Aw.Sindri.Features.StopProcess;

/// <summary>What was released.</summary>
/// <param name="Pid">The stopped process.</param>
/// <param name="Epoch">The released epoch (0 when it held none).</param>
public sealed record StopProcessResponse(int Pid, long Epoch);

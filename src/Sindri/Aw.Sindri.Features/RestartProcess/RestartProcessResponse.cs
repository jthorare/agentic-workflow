namespace Aw.Sindri.Features.RestartProcess;

/// <summary>What the restart released: the same fields as the stop's answer, so the 202 body is the same on the wire. The caller takes <paramref name="Pid"/> and waits for that process to exit.</summary>
/// <param name="Pid">The stopped process.</param>
/// <param name="Epoch">The released epoch (0 when it held none).</param>
/// <param name="Sequence">The 'process.stopped' event's ledger sequence, or null when nothing was recorded.</param>
internal sealed record RestartProcessResponse(int Pid, long Epoch, long? Sequence);

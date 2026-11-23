namespace Aw.Sindri.Core.Setup;

/// <summary>What a stop released: the old serve's pid, which the restart's caller waits on.</summary>
/// <param name="Pid">The stopped process.</param>
/// <param name="Epoch">The released epoch (0 when it held none).</param>
/// <param name="Sequence">The 'process.stopped' event's ledger sequence, or null when nothing was recorded.</param>
public sealed record ServeStopped(int Pid, long Epoch, long? Sequence);

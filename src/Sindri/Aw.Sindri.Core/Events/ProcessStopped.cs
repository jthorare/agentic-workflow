namespace Aw.Sindri.Core.Events;

/// <summary>The 'process.stopped' payload.</summary>
/// <param name="Pid">The process id.</param>
/// <param name="Epoch">The epoch it released.</param>
/// <param name="Reason">Why it stopped.</param>
public sealed record ProcessStopped(int Pid, long Epoch, StopReason Reason);

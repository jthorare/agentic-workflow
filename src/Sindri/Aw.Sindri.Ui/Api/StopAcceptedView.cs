namespace Aw.Sindri.Ui.Api;

/// <summary>The 202 of POST /sindri/stop: the process that was told to stop.</summary>
/// <param name="Pid">Its pid.</param>
/// <param name="Epoch">Its epoch.</param>
/// <param name="Sequence">The ledger sequence of the stop event.</param>
internal sealed record StopAcceptedView(int Pid, long Epoch, long Sequence);

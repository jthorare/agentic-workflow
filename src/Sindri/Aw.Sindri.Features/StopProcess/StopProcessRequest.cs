using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Features.StopProcess;

/// <summary>Stop the control plane: record the stop, release the lease, shut the process down.</summary>
/// <param name="Reason">Why: a client asked, a signal, or the port was taken.</param>
public sealed record StopProcessRequest(StopReason Reason);

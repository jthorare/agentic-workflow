using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Features.Launching;

/// <summary>A launch request the launcher service has read from the ledger and not yet given an outcome (UI spec E-1).</summary>
/// <param name="Request">The 'item.open-requested' payload.</param>
/// <param name="Sequence">The request's ledger sequence: the views must reach it before the launcher looks for an active instance (E-1).</param>
/// <param name="At">When the request was recorded: the 30 s deadline counts from here.</param>
public sealed record PendingLaunch(ItemOpenRequested Request, long Sequence, DateTimeOffset At);

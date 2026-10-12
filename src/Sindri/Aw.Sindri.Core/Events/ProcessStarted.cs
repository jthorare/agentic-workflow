using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'process.started' payload.</summary>
/// <param name="Host">The machine.</param>
/// <param name="Pid">The process id.</param>
/// <param name="Epoch">The lease epoch it holds.</param>
/// <param name="Profile">The approved profile hash it runs with, if any.</param>
public sealed record ProcessStarted(HostId Host, int Pid, long Epoch, ProfileHash? Profile);

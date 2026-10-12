using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Lease;

/// <summary>
/// Who holds (or asks for) the host lease: the machine, the process id, when that process started (whole seconds, so a
/// reused pid is told apart) and the boot session (so a pid from before a reboot is told apart).
/// </summary>
/// <param name="Host">The machine.</param>
/// <param name="Pid">The process id.</param>
/// <param name="PidStart">When the process started, truncated to whole seconds, UTC.</param>
/// <param name="BootId">The boot session id (macOS kern.bootsessionuuid, Linux boot_id).</param>
public sealed record LeaseHolder(HostId Host, int Pid, DateTimeOffset PidStart, BootId BootId)
{
    /// <summary>"host/pid", as messages show a holder.</summary>
    public override string ToString() => $"{Host}/{Pid}";
}

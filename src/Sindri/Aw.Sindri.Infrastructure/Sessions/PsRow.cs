namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>One live process of the current user as ps listed it.</summary>
/// <param name="Pid">The process id.</param>
/// <param name="ParentPid">Its parent.</param>
/// <param name="Command">The command name ps's comm column gives, as printed.</param>
/// <param name="Tty">0 for no terminal (ps's '??'), else nonzero.</param>
/// <param name="StartTicks">Its start time, epoch seconds.</param>
internal sealed record PsRow(int Pid, int ParentPid, string Command, int Tty, long StartTicks);

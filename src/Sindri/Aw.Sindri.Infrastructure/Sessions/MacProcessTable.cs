using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>
/// The process table on macOS (collector spec §4.1 steps 1, 2, 5, 6): ps for the table, the argument block for argv[0] and the environment,
/// lsof for the working directory and the open descriptors. Every tool runs from its absolute path.
/// </summary>
/// <param name="tools">Runs ps and lsof.</param>
/// <param name="arguments">Reads a process's argument block.</param>
/// <param name="ownUid">The uid that counts as ours.</param>
internal sealed class MacProcessTable(IMacTools tools, IMacProcessArguments arguments, int ownUid) : IProcessTable
{
    internal const string Ps = "/bin/ps";
    internal const string Lsof = "/usr/sbin/lsof";
    private const int FdLimit = 64;

    public Result<IReadOnlyList<ProcessEntry>> Read(CancellationToken cancellationToken)
    {
        var output = tools.Run(Ps, PsOutput.Arguments, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            return SindriErrors.ApiTimeout.With("the process table read was cancelled");
        }

        if (output is null)
        {
            return UnavailableReasons.ToError(UnavailableReason.UnsupportedPlatform);   // no ps here: neither /proc nor ps reads the table
        }

        var rows = PsOutput.Parse(output, ownUid);
        return rows.Count == 0
            ? UnavailableReasons.ToError(UnavailableReason.ProcUnreadable)
            : Results.Success<IReadOnlyList<ProcessEntry>>([.. rows.OrderBy(static r => r.Pid).Select(Entry)]);
    }

    public IReadOnlyDictionary<string, string> Environment(int pid) =>
        EnvironmentAllowlist.Filter(arguments.Read(pid)?.Environment ?? new Dictionary<string, string>(StringComparer.Ordinal));

    public string? Cwd(int pid) =>
        tools.Run(Lsof, ["-a", "-p", Number(pid), "-d", "cwd", "-F", "n"], CancellationToken.None) is { } output ? MacLsof.Cwd(output) : null;

    public IReadOnlyList<string> OpenFiles(int pid) =>
        tools.Run(Lsof, ["-a", "-p", Number(pid), "-d", "0-65535", "-F", "ftdn"], CancellationToken.None) is { } output ? MacLsof.OpenFiles(output, FdLimit) : [];

    private static string Number(int pid) => pid.ToString(CultureInfo.InvariantCulture);

    // why: ps's comm is the name the process was started as, a path for most and a title for some (a process may rewrite it), so only a path is an executable's
    private ProcessEntry Entry(PsRow row) => new(row.Pid, row.ParentPid, Name(row.Command), row.Tty, row.StartTicks)
    {
        Argv0 = arguments.Read(row.Pid)?.Argv0,
        ExePath = row.Command[0] == '/' ? row.Command : null,
    };

    private static string Name(string command) => command[0] == '/' ? command[(command.LastIndexOf('/') + 1)..] : command;
}

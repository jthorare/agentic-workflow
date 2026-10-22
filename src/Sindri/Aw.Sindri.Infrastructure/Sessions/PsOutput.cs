using System.Globalization;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>Reads "ps -axww -o pid=,ppid=,uid=,stat=,tty=,lstart=,comm=" run with TZ=UTC.</summary>
internal static class PsOutput
{
    /// <summary>The ps arguments the rows come from.</summary>
    internal static readonly string[] Arguments = ["-axww", "-o", "pid=,ppid=,uid=,stat=,tty=,lstart=,comm="];

    private const int PidColumn = 0;
    private const int ParentColumn = 1;
    private const int UidColumn = 2;
    private const int StatColumn = 3;
    private const int TtyColumn = 4;
    private const int StartFirstColumn = 5;
    private const int CommandColumn = 10;

    /// <summary>Parses the rows of one user, leaving zombies and unreadable lines out.</summary>
    /// <param name="output">ps's output.</param>
    /// <param name="ownUid">The uid that counts as ours.</param>
    /// <returns>The rows in the order ps printed them.</returns>
    internal static IReadOnlyList<PsRow> Parse(string output, int ownUid)
    {
        ArgumentNullException.ThrowIfNull(output);
        return [.. output.ReplaceLineEndings("\n").Split('\n').Select(line => Row(line, ownUid)).OfType<PsRow>()];
    }

    // why: the command is the last column and may hold spaces (a process title), so the line splits into one more column than ps has fixed ones and the rest is the command
    private static PsRow? Row(string line, int ownUid)
    {
        var columns = line.Split(' ', CommandColumn + 1, StringSplitOptions.RemoveEmptyEntries);
        if (columns.Length <= CommandColumn
            || !Number(columns[PidColumn], out var pid) || !Number(columns[ParentColumn], out var parent) || !Number(columns[UidColumn], out var uid) || uid != ownUid
            || columns[StatColumn][0] is 'Z' or 'X'
            || PsStartTime.ToEpochSeconds(string.Join(' ', columns[StartFirstColumn..CommandColumn])) is not { } started)
        {
            return null;
        }

        var command = columns[CommandColumn].Trim();
        return command.Length == 0 ? null : new PsRow(pid, parent, command, Terminal(columns[TtyColumn]), started);
    }

    // why: ProcessEntry.Tty only says whether there is a terminal (AgentProcessRules), so a name is the number in it plus one and ps's '??' is zero
    private static int Terminal(string tty)
    {
        if (string.Equals(tty, "??", StringComparison.Ordinal))
        {
            return 0;
        }

        var digits = tty.AsSpan(tty.AsSpan().LastIndexOfAnyExceptInRange('0', '9') + 1);
        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number < int.MaxValue ? number + 1 : 1;
    }

    private static bool Number(string text, out int value) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
}

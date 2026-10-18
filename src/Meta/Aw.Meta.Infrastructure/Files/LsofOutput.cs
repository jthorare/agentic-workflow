using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>Reads the field output of "lsof -F pcnR" (the macOS process scan).</summary>
internal static class LsofOutput
{
    /// <summary>One process lsof listed: its parent (R) and the location it holds.</summary>
    /// <param name="Ppid">The parent process id, 0 when lsof gave none.</param>
    /// <param name="Location">The process, its command and the absolute paths it holds.</param>
    internal sealed record Entry(int Ppid, ProcessLocation Location);

    /// <summary>Groups the p (pid), R (parent pid), c (command) and n (name) fields into one entry per process.</summary>
    /// <param name="output">The lsof output.</param>
    /// <returns>The processes with the absolute paths they hold.</returns>
    internal static IReadOnlyList<Entry> Parse(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        List<Entry> found = [];
        var (pid, ppid, command, paths) = (0, 0, string.Empty, new List<string>());
        foreach (var line in output.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            switch (line[0])
            {
                case 'p' when int.TryParse(line.AsSpan(1), out var next):
                    Flush();
                    (pid, ppid, command, paths) = (next, 0, string.Empty, []);
                    break;
                case 'R' when int.TryParse(line.AsSpan(1), out var parent):
                    ppid = parent;
                    break;
                case 'c':
                    command = line[1..];
                    break;
                case 'n' when pid > 0 && line.Length > 1 && line[1] == '/':
                    paths.Add(line[1..]);
                    break;
                default:
                    break;
            }
        }

        Flush();
        return found;

        void Flush()
        {
            if (pid > 0)
            {
                found.Add(new Entry(ppid, new ProcessLocation(pid, command, paths)));
            }
        }
    }
}

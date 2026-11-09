using System.Globalization;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Infrastructure.Todos;

/// <summary>Counts a note's hard links. .NET has no link count, so `stat` prints it (GNU: -c %h, BSD and macOS: -f %l).</summary>
internal sealed class NoteLinkCounter(ISandboxProcesses processes)
{
    /// <summary>Counts the links of one file.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The link count, or null when stat can't answer (the note can't be checked, so it is refused).</returns>
    internal int? Count(string path)
    {
        var run = processes.Run("/usr/bin/stat", OperatingSystem.IsMacOS() ? ["-f", "%l", path] : ["-c", "%h", path], TimeSpan.FromSeconds(5));
        return run.Succeeded && int.TryParse(run.Output.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var links) ? links : null;
    }
}

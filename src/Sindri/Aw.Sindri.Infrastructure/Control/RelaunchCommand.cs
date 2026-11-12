using System.Globalization;

namespace Aw.Sindri.Infrastructure.Control;

/// <summary>The command line of the restart's relauncher (#3052): wait for the old serve's pid to exit, at most 60 seconds, then start the installed build's serve.</summary>
internal static class RelaunchCommand
{
    /// <summary>The seconds the relauncher waits for the old serve to exit.</summary>
    internal const int WaitSeconds = 60;

    /// <summary>The installed sindri: 'bin/cs/current/sindri' beside the folder this process runs from; never this process's own path.</summary>
    /// <param name="baseDirectory">The folder the process runs from (AppContext.BaseDirectory).</param>
    /// <returns>The path, or null when the folder isn't '&lt;state&gt;/bin/cs/&lt;stamp&gt;'.</returns>
    internal static string? Installed(string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(baseDirectory);
        var folder = baseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var layout = Path.GetDirectoryName(folder);
        return layout is not null && Path.GetFileName(layout) == "cs" && Path.GetFileName(Path.GetDirectoryName(layout)) == "bin"
            ? Path.Combine(layout, "current", "sindri")
            : null;
    }

    /// <summary>The argv that starts '/bin/sh -c' with the wait-then-exec script.</summary>
    /// <param name="baseDirectory">The folder the process runs from.</param>
    /// <param name="pid">The serve that is stopping.</param>
    /// <param name="log">The serve log the new serve appends to.</param>
    /// <returns>The argv; null when <paramref name="baseDirectory"/> isn't in the install layout.</returns>
    internal static IReadOnlyList<string>? Argv(string baseDirectory, int pid, string log)
    {
        if (Installed(baseDirectory) is not { } installed)
        {
            return null;
        }

        var script = string.Create(CultureInfo.InvariantCulture,
            $"i=0; while kill -0 \"$1\" 2>/dev/null && [ \"$i\" -lt {WaitSeconds} ]; do sleep 1; i=$((i+1)); done; umask 077; exec \"$2\" serve </dev/null >>\"$3\" 2>&1");
        return ["/bin/sh", "-c", script, "sh", pid.ToString(CultureInfo.InvariantCulture), installed, log];
    }
}

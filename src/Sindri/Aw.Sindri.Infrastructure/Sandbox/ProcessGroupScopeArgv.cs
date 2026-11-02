namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>Builds the command that makes one session its own process group and records the group id where the watchdog reads it (macOS, SB-9c; the counterpart of <see cref="SystemdScopeArgv"/>). Pure.</summary>
internal static class ProcessGroupScopeArgv
{
    // $1 is the group id file, the rest is the inner command. tmux makes each pane's first process a session leader, so the shell is its own group leader; if it is not, the group id would name an unrelated group, so the start is refused.
    internal const string Script =
        "[ \"$(ps -o pgid= -p $$ | tr -d \" \")\" = \"$$\" ] || { echo \"sindri: the session is not its own process group\" >&2; exit 70; }; " +
        "mkdir -p \"${1%/*}\" && echo $$ > \"$1.tmp\" && mv \"$1.tmp\" \"$1\" && shift && exec \"$@\"";

    /// <summary>Builds the whole command.</summary>
    /// <param name="pgroupsDirectory">The folder of the group files (<see cref="ProcessGroupFiles.Directory"/>).</param>
    /// <param name="unit">The unit: the launch id.</param>
    /// <param name="inner">The command to run (the sandbox-exec command).</param>
    /// <returns>The argv.</returns>
    internal static IReadOnlyList<string> Start(string pgroupsDirectory, string unit, IReadOnlyList<string> inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        var file = ProcessGroupFiles.PgidPath(pgroupsDirectory, unit) ?? throw new ArgumentException("the unit is not a file name", nameof(unit));
        return ["/bin/sh", "-c", Script, "sh", file, .. inner];
    }

    /// <summary>Removes the unit's group id file left by an earlier start, then builds the command: if this start fails before it writes its own, a stale id must not name an unrelated group.</summary>
    /// <param name="pgroupsDirectory">The folder of the group files (<see cref="ProcessGroupFiles.Directory"/>).</param>
    /// <param name="unit">The unit: the launch id.</param>
    /// <param name="inner">The command to run (the sandbox-exec command).</param>
    /// <returns>The argv.</returns>
    internal static IReadOnlyList<string> Prepare(string pgroupsDirectory, string unit, IReadOnlyList<string> inner)
    {
        var argv = Start(pgroupsDirectory, unit, inner);
        ProcessGroupFiles.Remove(pgroupsDirectory, unit);
        return argv;
    }
}

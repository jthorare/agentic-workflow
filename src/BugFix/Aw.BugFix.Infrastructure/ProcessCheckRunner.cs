using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;

namespace Aw.BugFix.Infrastructure;

/// <summary>
/// Runs a test command as the program file argv[0] resolves to: started by the path the lookup found (so a venv's
/// python or an argv[0]-dispatched shim keeps its name) and hashed by that path's real file. It runs with the allowed
/// environment variables only, its output streamed to the log. A command that can't start or hits the time limit
/// didn't run to a verdict. At the limit, and after a normal exit, every process it started (a daemonized one too) is stopped:
/// SIGTERM, a grace period, then SIGKILL.
/// </summary>
internal sealed class ProcessCheckRunner(BugFixSettings settings, IFiles files) : ICheckRunner
{
    public CheckExit Run(IReadOnlyList<string> command, string directory, string log, string digestKey)
    {
        var environment = settings.FrozenVariables.Concat(settings.PassedVariables).ToDictionary(static kv => kv.Key, static kv => kv.Value, StringComparer.Ordinal);
        if (Found(command[0], directory) is not { } found || files.RealPath(found) is not { } real)
        {
            File.WriteAllText(log, $"bugfix-state: {command[0]} isn't a file in {directory} or on PATH; it can't start\n");
            return new CheckExit(Processes.CouldNotStart, Ran: false, string.Empty, CheckEnvironment.Digests(settings.FrozenVariables, digestKey));
        }

        var start = Processes.Start(found, command.Skip(1), directory);
        CheckEnvironment.Only(start.Environment, environment);
        var (code, ran) = Processes.ToLog(start, settings.CheckTimeout, settings.CheckGrace, log);
        return new CheckExit(code, ran, files.Sha256(real).Match(static s => s, static _ => string.Empty), CheckEnvironment.Digests(settings.FrozenVariables, digestKey));
    }

    // The path argv[0] names, as the OS would look it up from the run's directory, links not followed: relative to the
    // directory when it contains '/', otherwise the first PATH entry that has it.
    private string? Found(string name, string directory)
    {
        if (name.Contains('/', StringComparison.Ordinal))
        {
            return files.FullPath(directory, name);
        }

        return settings.FrozenVariables.GetValueOrDefault("PATH", string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(dir => files.FullPath(Path.IsPathRooted(dir) ? dir : Path.Join(directory, dir), name))
            .FirstOrDefault(p => files.RealPath(p) is not null);
    }
}

using System.Diagnostics;

namespace Aw.BugFix.Infrastructure;

/// <summary>Finds a sibling tool's binary the way the adapters promise: the variable's path when it is set, else the first PATH entry that has it.</summary>
internal static class ToolBinary
{
    // The state locations the tool reads, so it sees the same state this process does.
    private static readonly string[] _forwarded = ["AW_STATE_DIR", "AW_JUDGE_STATE_DIR"];

    /// <summary>Resolves <paramref name="name"/>; the error names the variable when it points at nothing or the program isn't on PATH.</summary>
    /// <param name="environment">The environment the variable and PATH are read from.</param>
    /// <param name="variable">The variable that overrides the lookup (AW_JUDGE_BIN).</param>
    /// <param name="name">The program name looked up on PATH.</param>
    /// <returns>The path, or null with a message.</returns>
    internal static (string? Path, string Message) Find(Func<string, string?> environment, string variable, string name)
    {
        if (environment(variable) is { Length: > 0 } configured)
        {
            return File.Exists(configured) ? (configured, string.Empty) : (null, $"{variable}={configured} isn't a file");
        }

        var found = (environment("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(dir => Path.Join(dir, name))
            .FirstOrDefault(File.Exists);
        return found is null ? (null, $"{name} isn't on PATH; install it or set {variable}") : (found, string.Empty);
    }

    /// <summary>Starts a process for the binary, with the state variables this process was given passed on.</summary>
    internal static ProcessStartInfo Start(Func<string, string?> environment, string path, IEnumerable<string> args, string? directory = null)
    {
        var start = Processes.Start(path, args, directory);
        foreach (var name in _forwarded)
        {
            if (environment(name) is { Length: > 0 } value)
            {
                start.Environment[name] = value;
            }
        }

        return start;
    }
}

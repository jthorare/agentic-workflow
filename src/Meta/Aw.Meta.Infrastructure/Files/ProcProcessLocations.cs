using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>
/// IProcessLocations on Linux over /proc: the working directory and every open file of each process the user may read. A process
/// that exits mid-scan, or belongs to someone else, is skipped; a scan that cannot run at all is an error.
/// </summary>
internal sealed class ProcProcessLocations : IProcessLocations
{
    private const string Proc = "/proc";

    public ValueTask<Result<IReadOnlyList<ProcessLocation>>> Locations(CancellationToken cancellationToken)
    {
        try
        {
            return ValueTask.FromResult(Results.Success<IReadOnlyList<ProcessLocation>>(Scan()));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            return ValueTask.FromResult(Results.Failure<IReadOnlyList<ProcessLocation>>(MetaErrors.HostUnreadable.With("the process scan failed: " + exception.Message)));
        }
    }

    private static List<ProcessLocation> Scan()
    {
        var parents = new Dictionary<int, int>();
        var directories = new List<(int Pid, string Directory)>();
        foreach (var directory in Directory.EnumerateDirectories(Proc))
        {
            if (int.TryParse(Path.GetFileName(directory), NumberStyles.None, CultureInfo.InvariantCulture, out var pid))
            {
                directories.Add((pid, directory));
                if (Parent(directory) is { } parent)
                {
                    parents[pid] = parent;
                }
            }
        }

        var invoker = ProcessAncestry.Chain(Environment.ProcessId, parents);
        var found = new List<ProcessLocation>();
        foreach (var (pid, directory) in directories)
        {
            if (Read(pid, directory, invoker.Contains(pid)) is { } location)
            {
                found.Add(location);
            }
        }

        return found;
    }

    private static int? Parent(string directory)
    {
        try
        {
            return ProcFormat.Stat(File.ReadAllText(Path.Combine(directory, "stat")))?.Ppid;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            return null;
        }
    }

    // A process the user can't read (another user's, or gone) holds nothing we could be asked about: skipped.
    private static ProcessLocation? Read(int pid, string directory, bool invoker)
    {
        try
        {
            var paths = new List<string>();
            AddTarget(paths, Path.Combine(directory, "cwd"));
            foreach (var descriptor in Directory.EnumerateFileSystemEntries(Path.Combine(directory, "fd")))
            {
                AddTarget(paths, descriptor);
            }

            var command = File.ReadAllText(Path.Combine(directory, "cmdline")).Replace('\0', ' ').Trim();
            return new ProcessLocation(pid, command.Length > 0 ? command : "[" + pid.ToString(CultureInfo.InvariantCulture) + "]", paths, invoker);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void AddTarget(List<string> paths, string link)
    {
        if (new FileInfo(link).LinkTarget is { Length: > 0 } target && target[0] == '/')
        {
            paths.Add(target);
        }
    }
}

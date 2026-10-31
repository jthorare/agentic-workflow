using System.Text.RegularExpressions;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>What the start-up sweep removes of a dead launch's process-group files and macOS temp folder (SB-9c). Neither sweep follows a symlink.</summary>
internal static partial class StaleSandboxFiles
{
    /// <summary>Deletes the '.pgid' files of units whose group is gone and whose launch has no live session.</summary>
    /// <param name="directory">The folder of the group files.</param>
    /// <param name="live">Whether a launch has a live session.</param>
    /// <param name="alive">Whether a process group has a process.</param>
    /// <returns>How many files were removed.</returns>
    internal static int SweepGroupFiles(string directory, Func<LaunchId, bool> live, Func<int, bool> alive)
    {
        ArgumentNullException.ThrowIfNull(live);
        ArgumentNullException.ThrowIfNull(alive);
        var removed = 0;
        foreach (var unit in ProcessGroupFiles.Units(directory))
        {
            if (!LaunchId.TryParse(unit, provider: null, out var launch) || live(launch))
            {
                continue;
            }

            if (ProcessGroupFiles.ReadPgid(directory, unit) is { } pgid && alive(pgid))
            {
                continue;   // why: a group that still has a process may be a session the sweep must not orphan
            }

            ProcessGroupFiles.Remove(directory, unit);
            removed++;
        }

        return removed;
    }

    /// <summary>Deletes the 'aw-&lt;8 hex&gt;' folders of launches that are not live from the temp root.</summary>
    /// <param name="root">The temp root (/private/tmp).</param>
    /// <param name="liveFolders">The folder names of live launches.</param>
    /// <param name="owned">Whether this user owns the folder.</param>
    /// <returns>How many folders were removed.</returns>
    internal static int SweepSeatbeltTemp(string root, IReadOnlySet<string> liveFolders, Func<string, bool> owned)
    {
        ArgumentNullException.ThrowIfNull(liveFolders);
        ArgumentNullException.ThrowIfNull(owned);
        if (!Directory.Exists(root))
        {
            return 0;
        }

        var removed = 0;
        foreach (var path in Directory.EnumerateDirectories(root))
        {
            var name = Path.GetFileName(path);
            if (!TempName.IsMatch(name) || liveFolders.Contains(name) || new DirectoryInfo(path).LinkTarget is not null || !owned(path))
            {
                continue;
            }

            try
            {
                Directory.Delete(path, recursive: true);   // why: Directory.Delete removes a symlink inside the folder, never what it points at
                removed++;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // best effort: the next start-up tries again
            }
        }

        return removed;
    }

    [GeneratedRegex("^aw-[0-9a-f]{8}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TempName { get; }
}

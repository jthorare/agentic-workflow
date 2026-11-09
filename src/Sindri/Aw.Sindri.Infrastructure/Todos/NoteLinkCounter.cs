using System.Collections.Concurrent;
using System.Globalization;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Infrastructure.Todos;

/// <summary>
/// Counts a note's hard links. .NET has no link count, so `stat` prints it (GNU: -c %h, BSD and macOS: -f %l). The answer is kept per path
/// with the note's mtime and size: an idle pass on an unchanged note starts no process, a changed note is counted again.
/// The key has no inode or ctime (.NET exposes neither): a link added by `ln`, or a file swapped in with the same mtime and size
/// (`cp -p`), keeps the old count until the note changes. Writes still go only through the swap, so a missed link is never written through.
/// </summary>
internal sealed class NoteLinkCounter(ISandboxProcesses processes)
{
    private readonly ConcurrentDictionary<string, (DateTime Written, long Length, int Links)> _seen = new(StringComparer.Ordinal);

    /// <summary>Counts the links of one file.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The link count, or null when stat can't answer (the note can't be checked, so it is refused).</returns>
    internal int? Count(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            return Ask(path);
        }

        var (written, length) = (info.LastWriteTimeUtc, info.Length);
        if (_seen.TryGetValue(path, out var known) && known.Written == written && known.Length == length)
        {
            return known.Links;
        }

        var links = Ask(path);
        if (links is { } counted)
        {
            _seen[path] = (written, length, counted);
        }
        else
        {
            _ = _seen.TryRemove(path, out _);   // a failed answer is never kept
        }

        return links;
    }

    private int? Ask(string path)
    {
        var run = processes.Run("/usr/bin/stat", OperatingSystem.IsMacOS() ? ["-f", "%l", path] : ["-c", "%h", path], TimeSpan.FromSeconds(5));
        return run.Succeeded && int.TryParse(run.Output.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var links) ? links : null;
    }
}

using Aw.Sindri.Core.Judge;

namespace Aw.Sindri.Infrastructure.Judge;

/// <summary>
/// <see cref="IStepImageFiles"/> over the file system. A path is resolved part by part from the root, the way the OS would open it: a symlink at any
/// part (a directory above the file as much as the file itself) is replaced by its target, a relative target is read against the real directory it
/// sits in, and a '..' pops the real directory, never a name that was only read as text. Managed code only, so macOS resolves the same way.
/// </summary>
internal sealed class StepImageFiles : IStepImageFiles
{
    private const int MaxHops = 40;

    /// <inheritdoc/>
    public string? RealPath(string path) => File.Exists(path) ? Walk(Path.GetFullPath(path)) : null;

    /// <inheritdoc/>
    public string RealDirectory(string directory)
    {
        var full = Path.GetFullPath(directory);
        return Walk(full) ?? full;
    }

    // Null when the links loop (more than MaxHops of them), which reads as "not there".
    private static string? Walk(string full)
    {
        var root = Path.GetPathRoot(full) ?? string.Empty;
        var pending = new Stack<string>();
        Push(pending, full[root.Length..]);
        var current = root;
        var hops = 0;
        while (pending.Count > 0)
        {
            var part = pending.Pop();
            if (string.Equals(part, "..", StringComparison.Ordinal))
            {
                current = Path.GetDirectoryName(current.TrimEnd(Path.DirectorySeparatorChar)) is { Length: > 0 } parent ? parent : root;
                continue;
            }

            var next = Path.Combine(current, part);
            var target = LinkTarget(next);
            if (target is null)
            {
                current = next;
                continue;
            }

            hops++;
            if (hops > MaxHops)
            {
                return null;
            }

            current = Follow(target, current, pending);
        }

        return current;
    }

    // An absolute target restarts the walk at its own root; a relative one is read against the real directory the link sits in, which is `current`.
    private static string Follow(string target, string current, Stack<string> pending)
    {
        if (!Path.IsPathRooted(target))
        {
            Push(pending, target);
            return current;
        }

        var root = Path.GetPathRoot(target) ?? string.Empty;
        Push(pending, target[root.Length..]);
        return root;
    }

    // The parts of a path, so the first is popped first.
    private static void Push(Stack<string> pending, string path)
    {
        foreach (var part in path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries).Where(static p => !string.Equals(p, ".", StringComparison.Ordinal)).Reverse())
        {
            pending.Push(part);
        }
    }

    private static string? LinkTarget(string path)
    {
        try
        {
            return new FileInfo(path).LinkTarget;
        }
        catch (IOException)
        {
            return null;   // boundary: a part that can't be read counts as a plain name, which the existence check has vouched for
        }
    }
}

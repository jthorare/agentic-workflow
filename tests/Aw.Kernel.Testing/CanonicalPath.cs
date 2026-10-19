namespace Aw.Kernel.Testing;

/// <summary>
/// An existing path with every symlink resolved, for comparing what a test built with what a process or the code under
/// test reports (getcwd, 'pwd -P', a real-path lookup). macOS keeps its temp directories under '/var/folders', a link to
/// '/private/var/folders', so a temp path and the same path as a process sees it differ; on Linux this changes nothing.
/// </summary>
public static class CanonicalPath
{
    /// <summary>Resolves each segment of <paramref name="path"/> that is a link, so no part of the result is one.</summary>
    /// <param name="path">An existing absolute path.</param>
    /// <returns>The path without links (a path that doesn't exist keeps its unresolved tail).</returns>
    public static string Of(string path)
    {
        var full = Path.GetFullPath(path);
        var current = Path.GetPathRoot(full) ?? string.Empty;
        foreach (var segment in full[current.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            FileSystemInfo entry = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            var target = entry.Exists ? entry.ResolveLinkTarget(returnFinalTarget: true) : null;
            if (target is not null)
            {
                current = target.FullName;
            }
        }

        return current;
    }
}

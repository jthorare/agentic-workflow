namespace Aw.Context.Testing.Fixtures;

/// <summary>
/// Builds a fixture repo once per process and hands out copies. A fixture's history is fixed (author, committer and dates are pinned), so a copy has the same shas as a fresh build,
/// and the twenty-odd git processes a build costs are paid once. A copy is a new directory tree: the template is never given out, so a test that disposes its repo removes only its own.
/// The one absolute path in a built repo is core.hooksPath in .git/config (it names a folder under the repo's root), which is rewritten to the copy's root.
/// </summary>
internal static class RepoTemplate
{
    private static readonly Lock _gate = new();
    private static readonly Dictionary<string, (string Root, object Built)> _built = new(StringComparer.Ordinal);
    private static bool _cleanupRegistered;

    /// <summary>Copies the template named <paramref name="key"/> into <paramref name="root"/>, building it first if this process has not.</summary>
    /// <typeparam name="T">What the build returns (the repo's head and file lists: nothing that names a path).</typeparam>
    /// <param name="key">The template's name.</param>
    /// <param name="prefix">The temp folder prefix for the template's own folder.</param>
    /// <param name="root">The new, empty folder to copy into.</param>
    /// <param name="build">Builds the repo into the (empty, existing) folder it is given and returns what describes it.</param>
    /// <returns>What <paramref name="build"/> returned for the template.</returns>
    internal static T Copy<T>(string key, string prefix, string root, Func<string, T> build)
        where T : notnull
    {
        (string Root, object Built) template;
        lock (_gate)
        {
            if (!_built.TryGetValue(key, out template))
            {
                var templateRoot = Directory.CreateTempSubdirectory("aw-template-" + prefix).FullName;
                try
                {
                    template = (templateRoot, build(templateRoot));
                }
                catch
                {
                    Directory.Delete(templateRoot, recursive: true);
                    throw;
                }

                _built[key] = template;
                if (!_cleanupRegistered)
                {
                    _cleanupRegistered = true;
                    AppDomain.CurrentDomain.ProcessExit += (_, _) => DeleteTemplates();
                }
            }
        }

        CopyTree(template.Root, root);
        return (T)template.Built;
    }

    private static void CopyTree(string from, string to)
    {
        foreach (var directory in Directory.EnumerateDirectories(from, "*", SearchOption.AllDirectories))
        {
            _ = Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            File.Copy(file, target);
            File.SetLastWriteTimeUtc(target, File.GetLastWriteTimeUtc(file));   // git's index compares file times: a copy that keeps them stays clean without a rehash
            if (string.Equals(Path.GetFileName(file), "config", StringComparison.Ordinal) && string.Equals(Path.GetFileName(Path.GetDirectoryName(file)), ".git", StringComparison.Ordinal))
            {
                File.WriteAllText(target, File.ReadAllText(file).Replace(from, to, StringComparison.Ordinal));
            }
        }
    }

    private static void DeleteTemplates()
    {
        foreach (var (root, _) in _built.Values)
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // left in the temp folder; the process is ending
            }
        }
    }
}

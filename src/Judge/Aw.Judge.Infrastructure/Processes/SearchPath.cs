using Aw.Kernel.Data;

namespace Aw.Judge.Infrastructure.Processes;

/// <summary>Finds an executable in the PATH directories: a regular file with an execute bit (any file on Windows).</summary>
internal static class SearchPath
{
    /// <summary>The first executable of this name on the PATH, or null.</summary>
    /// <param name="path">The PATH value (null when unset).</param>
    /// <param name="name">The binary name.</param>
    /// <returns>Its full path, or null.</returns>
    internal static string? Find(string? path, string name) =>
        (path ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, name))
            .FirstOrDefault(ExecutableFiles.IsExecutable);
}

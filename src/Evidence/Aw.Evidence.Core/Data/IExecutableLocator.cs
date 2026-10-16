namespace Aw.Evidence.Core.Data;

/// <summary>Resolves paths on disk the way a CLI target is vetted: every symlink followed, nothing looked up on PATH.</summary>
public interface IExecutableLocator
{
    /// <summary>The real path of an executable file.</summary>
    /// <param name="path">An absolute path.</param>
    /// <returns>The path with every symlink followed, or null when it is not an executable file.</returns>
    public string? RealExecutable(string path);

    /// <summary>The real path of a regular file, executable or not.</summary>
    /// <param name="path">An absolute path.</param>
    /// <returns>The path with every symlink followed, or null when it is not a file.</returns>
    public string? RealFile(string path);

    /// <summary>The real path of a directory.</summary>
    /// <param name="path">An absolute path.</param>
    /// <returns>The path with every symlink followed, or null when it is not a directory.</returns>
    public string? RealDirectory(string path);
}

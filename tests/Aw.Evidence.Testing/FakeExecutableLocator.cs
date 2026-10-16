using Aw.Evidence.Core.Data;

namespace Aw.Evidence.Testing;

/// <summary>An IExecutableLocator over a map of paths: each executable and directory is declared with the real path it resolves to; every query is recorded.</summary>
public sealed class FakeExecutableLocator : IExecutableLocator
{
    private readonly Dictionary<string, string> _executables = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _directories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);
    private readonly List<string> _queries = [];

    /// <summary>Every path asked about, in order.</summary>
    public IReadOnlyList<string> Queries => _queries;

    /// <summary>Declares an executable file; it resolves to itself.</summary>
    /// <param name="path">The path.</param>
    /// <returns>This fake.</returns>
    public FakeExecutableLocator Executable(string path) => Executable(path, path);

    /// <summary>Declares an executable file that resolves (a symlink) to <paramref name="real"/>.</summary>
    /// <param name="path">The path asked about.</param>
    /// <param name="real">Where it really is.</param>
    /// <returns>This fake.</returns>
    public FakeExecutableLocator Executable(string path, string real)
    {
        _executables[path] = real;
        return this;
    }

    /// <summary>Declares a directory; it resolves to itself.</summary>
    /// <param name="path">The path.</param>
    /// <returns>This fake.</returns>
    public FakeExecutableLocator Directory(string path) => Directory(path, path);

    /// <summary>Declares a directory that resolves (a symlink) to <paramref name="real"/>.</summary>
    /// <param name="path">The path asked about.</param>
    /// <param name="real">Where it really is.</param>
    /// <returns>This fake.</returns>
    public FakeExecutableLocator Directory(string path, string real)
    {
        _directories[path] = real;
        return this;
    }

    /// <inheritdoc/>
    public string? RealExecutable(string path)
    {
        _queries.Add(path);
        return _executables.GetValueOrDefault(path);
    }

    /// <summary>Makes <paramref name="path"/> a regular file whose real path is <paramref name="real"/>.</summary>
    /// <param name="path">The path asked about.</param>
    /// <param name="real">Where it really is (equal to the path when no link is involved).</param>
    /// <returns>This fake.</returns>
    public FakeExecutableLocator File(string path, string? real = null)
    {
        _files[path] = real ?? path;
        return this;
    }

    /// <inheritdoc/>
    public string? RealFile(string path)
    {
        _queries.Add(path);
        return _files.GetValueOrDefault(path);
    }

    /// <inheritdoc/>
    public string? RealDirectory(string path)
    {
        _queries.Add(path);
        return _directories.GetValueOrDefault(path);
    }
}

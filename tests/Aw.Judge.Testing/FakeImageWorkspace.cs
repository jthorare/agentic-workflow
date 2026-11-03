using Aw.Judge.Core.Images;

namespace Aw.Judge.Testing;

/// <summary>An in-memory image directory made by <see cref="FakeImageWorkspaces"/>.</summary>
public sealed class FakeImageWorkspace : IImageWorkspace
{
    private readonly Dictionary<string, byte[]> _files;

    internal FakeImageWorkspace(string path, Dictionary<string, byte[]> files)
    {
        Path = path;
        _files = files;
        Files = [.. files.Keys];
    }

    /// <inheritdoc />
    public string Path { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> Files { get; }

    /// <summary>Whether it was disposed (removed).</summary>
    public bool Disposed { get; private set; }

    /// <summary>A file's bytes.</summary>
    /// <param name="name">The name in <see cref="Files"/>.</param>
    /// <returns>The bytes, or null when there is no such file or the workspace was removed.</returns>
    public byte[]? Read(string name) => !Disposed && _files.TryGetValue(name, out var bytes) ? bytes : null;

    /// <inheritdoc />
    public void Dispose() => Disposed = true;
}

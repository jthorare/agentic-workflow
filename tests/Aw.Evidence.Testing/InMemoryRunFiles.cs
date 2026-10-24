using Aw.Evidence.Core;
using Aw.Evidence.Core.Files;
using Aw.Kernel;

namespace Aw.Evidence.Testing;

/// <summary>An in-memory IRunFiles: files by absolute path, plus the directories created.</summary>
public sealed class InMemoryRunFiles : IRunFiles
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);
    private readonly HashSet<string> _directories = new(StringComparer.Ordinal);

    /// <summary>Paths that fail every operation with AW-EVIDENCE-011.</summary>
    public ISet<string> Broken { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Every file, by path.</summary>
    public IReadOnlyDictionary<string, byte[]> Files => _files;

    /// <summary>Every directory created.</summary>
    public IReadOnlyCollection<string> Directories => _directories;

    /// <summary>Puts a file in place without going through the port.</summary>
    /// <param name="path">The path.</param>
    /// <param name="content">The bytes.</param>
    /// <returns>This store.</returns>
    public InMemoryRunFiles With(string path, byte[] content)
    {
        _files[path] = content;
        return this;
    }

    /// <summary>Takes a file away without going through the port.</summary>
    /// <param name="path">The path.</param>
    /// <returns>This store.</returns>
    public InMemoryRunFiles Without(string path)
    {
        _ = _files.Remove(path);
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<byte[]>> ReadAsync(string path, CancellationToken cancellationToken)
    {
        Result<byte[]> result = Broken.Contains(path) ? Unavailable(path)
            : _files.TryGetValue(path, out var bytes) ? bytes.ToArray()
            : EvidenceErrors.FileNotFound.With($"{path} does not exist");
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> WriteAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        if (Broken.Contains(path))
        {
            return ValueTask.FromResult<Result<Unit>>(Unavailable(path));
        }

        _files[path] = content.ToArray();
        _ = _directories.Add(RunDirectoryOf(path));
        return ValueTask.FromResult(Results.Ok());
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> DeleteAsync(string path, CancellationToken cancellationToken)
    {
        if (Broken.Contains(path))
        {
            return ValueTask.FromResult<Result<Unit>>(Unavailable(path));
        }

        _ = _files.Remove(path);
        return ValueTask.FromResult(Results.Ok());
    }

    /// <inheritdoc/>
    public ValueTask<Result<long>> SizeAsync(string path, CancellationToken cancellationToken)
    {
        Result<long> result = Broken.Contains(path) ? Unavailable(path)
            : _files.TryGetValue(path, out var bytes) ? bytes.LongLength
            : EvidenceErrors.FileNotFound.With($"{path} does not exist");
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> CreateDirectoryAsync(string path, CancellationToken cancellationToken)
    {
        if (Broken.Contains(path))
        {
            return ValueTask.FromResult<Result<Unit>>(Unavailable(path));
        }

        _ = _directories.Add(path.TrimEnd('/'));
        return ValueTask.FromResult(Results.Ok());
    }

    private static Error Unavailable(string path) => EvidenceErrors.FileUnavailable.With($"{path} is unavailable");

    private static string RunDirectoryOf(string path) => path[..Math.Max(0, path.LastIndexOf('/'))];
}

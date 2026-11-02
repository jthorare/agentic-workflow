using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Infrastructure.Workflows;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>An <see cref="IStepOutputs"/> in memory: the files of each run directory's output folder, as the test puts them, each with the time it was written. A run directory the test put nothing in is read from disk, so a test that writes real files needs no fake.</summary>
internal sealed class FakeStepOutputs : IStepOutputs
{
    private readonly DiskStepOutputs _disk = new();
    private readonly Dictionary<string, Dictionary<string, (byte[] Content, DateTimeOffset At)>> _folders = new(StringComparer.Ordinal);

    /// <summary>Puts a file in <paramref name="runDirectory"/>'s output folder.</summary>
    /// <param name="runDirectory">The run directory.</param>
    /// <param name="name">The file name.</param>
    /// <param name="content">Its text.</param>
    /// <param name="at">When it was written; the far future by default, so it is newer than any launch.</param>
    public void Add(string runDirectory, string name, string content, DateTimeOffset? at = null)
    {
        if (!_folders.TryGetValue(runDirectory, out var folder))
        {
            _folders[runDirectory] = folder = new Dictionary<string, (byte[], DateTimeOffset)>(StringComparer.Ordinal);
        }

        folder[name] = (System.Text.Encoding.UTF8.GetBytes(content), at ?? DateTimeOffset.MaxValue);
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<string>>> Names(string runDirectory, DateTimeOffset? since, CancellationToken cancellationToken)
    {
        if (!_folders.ContainsKey(runDirectory))
        {
            return _disk.Names(runDirectory, since, cancellationToken);
        }

        IReadOnlyList<string> names = _folders.TryGetValue(runDirectory, out var folder)
            ? [.. folder.Where(f => since is not { } from || f.Value.At >= from).Select(static f => f.Key).Order(StringComparer.Ordinal)]
            : [];
        return ValueTask.FromResult(Results.Success(names));
    }

    /// <inheritdoc/>
    public ValueTask<Result<ReadOnlyMemory<byte>>> Read(string runDirectory, string name, CancellationToken cancellationToken) =>
        !_folders.ContainsKey(runDirectory) ? _disk.Read(runDirectory, name, cancellationToken) : ValueTask.FromResult(_folders.TryGetValue(runDirectory, out var folder) && folder.TryGetValue(name, out var file)
            ? Results.Success(new ReadOnlyMemory<byte>(file.Content))
            : (Result<ReadOnlyMemory<byte>>)SindriErrors.InvalidInput.With($"{name} is not in {runDirectory}"));
}

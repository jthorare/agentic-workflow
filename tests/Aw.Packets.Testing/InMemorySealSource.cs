using Aw.Kernel;
using Aw.Packets.Core;
using Aw.Packets.Core.Values;

namespace Aw.Packets.Testing;

/// <summary>The in-memory <see cref="ISealSource"/>: seal files as bytes by path, parsed by the same reader as the disk adapter.</summary>
internal sealed class InMemorySealSource : ISealSource
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    /// <summary>Adds (or replaces) a file.</summary>
    /// <param name="path">The path.</param>
    /// <param name="bytes">Its bytes.</param>
    /// <returns>This fake.</returns>
    public InMemorySealSource With(string path, byte[] bytes)
    {
        _files[path] = bytes;
        return this;
    }

    /// <inheritdoc />
    public ValueTask<Result<PacketHash>> Read(string path, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_files.TryGetValue(path, out var bytes)
            ? SealReader.Parse(path, bytes)
            : PacketsErrors.SealUnusable.With($"No seal file at {path}."));
}

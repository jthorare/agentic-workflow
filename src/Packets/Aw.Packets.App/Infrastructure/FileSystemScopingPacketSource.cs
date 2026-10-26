using Aw.Kernel;
using Aw.Packets.Core;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Core.Kinds.Scoping;

namespace Aw.Packets.App.Infrastructure;

/// <summary>
/// Reads a scope packet directory from disk: the fourteen files the scoping kind lists (a symbolic link is refused rather
/// than followed), and the names of every other top-level entry. Nothing in a sub-directory is read.
/// </summary>
internal sealed class FileSystemScopingPacketSource : IScopingPacketSource
{
    public ValueTask<Result<ScopingFiles>> Read(string packetDirectory, CancellationToken cancellationToken)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(packetDirectory));
        return ValueTask.FromResult(Directory.Exists(root)
            ? ReadDirectory(root)
            : PacketsErrors.NotFound.With($"No packet directory at {packetDirectory}."));
    }

    private static Result<ScopingFiles> ReadDirectory(string root)
    {
        var present = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal);
        foreach (var spec in PacketKinds.Scoping.Files)
        {
            var bytes = FileSystemPacketSource.ReadFile(root, spec.Name);
            if (bytes.IsError)
            {
                return bytes.Error;
            }

            if (bytes.Value is { } found)
            {
                present[spec.Name] = found;
            }
        }

        try
        {
            var unlisted = new DirectoryInfo(root).EnumerateFileSystemInfos()
                .Where(entry => !present.ContainsKey(entry.Name) && !PacketKinds.Scoping.Files.Any(f => string.Equals(f.Name, entry.Name, StringComparison.Ordinal)))
                .Select(static entry => entry is DirectoryInfo && entry.LinkTarget is null ? entry.Name + "/" : entry.Name)
                .Order(StringComparer.Ordinal)
                .ToList();
            return new ScopingFiles(present, unlisted);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return PacketsErrors.Unreadable.With($"The packet directory can't be listed: {ex.Message}");
        }
    }
}

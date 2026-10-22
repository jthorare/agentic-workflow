using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.LensPacks;

namespace Aw.Sindri.Infrastructure.LensPacks;

/// <summary><see cref="IPackDirectories"/> over the disk: every file under the directory; a link whose target is outside it is named and not read, and a link is never walked into.</summary>
internal sealed class DiskPackDirectories : IPackDirectories
{
    private const int MaxFiles = 500;

    public async ValueTask<Result<PackDirectory>> Read(string directory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directory);
        var root = Path.GetFullPath(directory);
        if (!Directory.Exists(root))
        {
            return SindriErrors.InvalidInput.With($"{directory} is not a directory");
        }

        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        var outside = new List<string>();
        try
        {
            foreach (var entry in new DirectoryInfo(root).EnumerateFileSystemInfos("*", new EnumerationOptions { RecurseSubdirectories = false }))
            {
                await Visit(entry, root, (files, outside), cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)   // boundary: an unreadable pack directory is the answer, not a crash
        {
            return SindriErrors.InvalidInput.With($"{directory} can't be read: {e.Message}");
        }

        return files.Count > MaxFiles
            ? SindriErrors.InvalidInput.With($"{directory} holds more than {MaxFiles} files, which is not a lens pack")
            : new PackDirectory(files, [.. outside.Order(StringComparer.Ordinal)]);
    }

    private static async ValueTask Visit(FileSystemInfo entry, string root, (Dictionary<string, string> Files, List<string> Outside) found, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var relative = Path.GetRelativePath(root, entry.FullName).Replace('\\', '/');
        if (entry.LinkTarget is not null)
        {
            var target = entry.ResolveLinkTarget(returnFinalTarget: true);
            if (target is null || !target.Exists || !Inside(root, target.FullName))
            {
                found.Outside.Add(relative);
                return;
            }
        }

        if (entry is DirectoryInfo directory)
        {
            if (entry.LinkTarget is null)
            {
                foreach (var child in directory.EnumerateFileSystemInfos())
                {
                    await Visit(child, root, found, cancellationToken).ConfigureAwait(false);
                }
            }

            return;
        }

        if (found.Files.Count <= MaxFiles)
        {
            found.Files[relative] = await File.ReadAllTextAsync(entry.FullName, cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool Inside(string root, string path) => path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);
}

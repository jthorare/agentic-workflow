using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>IWorktreeFiles over the file system: the directory's own time and its entries' times, two levels deep.</summary>
internal sealed class DirectoryWorktreeFiles : IWorktreeFiles
{
    public ValueTask<Result<DateTimeOffset>> NewestChange(string directory, IReadOnlyCollection<string> skipNames, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(skipNames);
        try
        {
            var newest = Directory.GetLastWriteTimeUtc(directory);
            foreach (var first in Directory.EnumerateFileSystemEntries(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (skipNames.Contains(Path.GetFileName(first), StringComparer.Ordinal))
                {
                    continue;
                }

                newest = Max(newest, Time(first));
                if (Directory.Exists(first) && !File.GetAttributes(first).HasFlag(FileAttributes.ReparsePoint))
                {
                    newest = Max(newest, Children(first, skipNames));
                }
            }

            return ValueTask.FromResult(Results.Success(new DateTimeOffset(newest, TimeSpan.Zero)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ValueTask.FromResult(Results.Failure<DateTimeOffset>(MetaErrors.CommandFailed.With("could not read " + directory + ": " + exception.Message)));
        }
    }

    private static DateTime Children(string directory, IReadOnlyCollection<string> skipNames)
    {
        var newest = DateTime.MinValue;
        foreach (var second in Directory.EnumerateFileSystemEntries(directory))
        {
            if (!skipNames.Contains(Path.GetFileName(second), StringComparer.Ordinal))
            {
                newest = Max(newest, Time(second));
            }
        }

        return newest;
    }

    private static DateTime Time(string path) => Directory.Exists(path) ? Directory.GetLastWriteTimeUtc(path) : File.GetLastWriteTimeUtc(path);

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
}

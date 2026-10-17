namespace Aw.Sindri.Infrastructure.Launch.Usage;

/// <summary>Finds an agent CLI's transcript files and reads their lines; a missing directory is no transcripts, never an error.</summary>
internal static class TranscriptFiles
{
    /// <summary>The '.jsonl' files under <paramref name="root"/> last written at or after <paramref name="since"/> (a file untouched since can hold nothing newer), in path order.</summary>
    /// <param name="root">The transcripts' root.</param>
    /// <param name="since">The earliest write time, or null for every file.</param>
    /// <returns>The paths.</returns>
    public static IReadOnlyList<string> Jsonl(string root, DateTimeOffset? since)
    {
        if (!Directory.Exists(root))
        {
            return [];
        }

        var found = new List<string>();
        try
        {
            foreach (var path in Directory.EnumerateFiles(root, "*.jsonl", SearchOption.AllDirectories))
            {
                if (since is not { } earliest || File.GetLastWriteTimeUtc(path) >= earliest.UtcDateTime)
                {
                    found.Add(path);
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // a directory that vanished or can't be read mid-walk: what was found stands
        }

        found.Sort(StringComparer.Ordinal);
        return found;
    }

    /// <summary>The lines of <paramref name="path"/> that contain <paramref name="marker"/>; a file that can't be read has none.</summary>
    /// <param name="path">The file.</param>
    /// <param name="marker">A cheap pre-filter: lines without it are never parsed.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The lines.</returns>
    public static async ValueTask<IReadOnlyList<string>> Lines(string path, string marker, CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        try
        {
            await foreach (var line in File.ReadLinesAsync(path, cancellationToken).ConfigureAwait(false))
            {
                if (line.Contains(marker, StringComparison.Ordinal))
                {
                    lines.Add(line);
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // a transcript that vanished while it was read: what was read stands
        }

        return lines;
    }
}

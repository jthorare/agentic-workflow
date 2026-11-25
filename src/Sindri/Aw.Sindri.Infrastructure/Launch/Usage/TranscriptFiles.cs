using System.Buffers;
using System.Text;

namespace Aw.Sindri.Infrastructure.Launch.Usage;

/// <summary>Finds an agent CLI's transcript files and reads their lines; a missing directory is no transcripts, never an error.</summary>
internal static class TranscriptFiles
{
    /// <summary>The '.jsonl' files under <paramref name="root"/> last written at or after <paramref name="since"/> (a file untouched since can hold nothing newer), in path order.</summary>
    /// <param name="root">The transcripts' root.</param>
    /// <param name="since">The earliest write time, or null for every file.</param>
    /// <returns>The paths.</returns>
    public static IReadOnlyList<string> Jsonl(string root, DateTimeOffset? since) => Jsonl([root], since);

    /// <summary>The same over several roots (the owner's and each separate agent home's), one sorted list.</summary>
    /// <param name="roots">The transcripts' roots; one that is missing has none.</param>
    /// <param name="since">The earliest write time, or null for every file.</param>
    /// <returns>The paths.</returns>
    public static IReadOnlyList<string> Jsonl(IReadOnlyList<string> roots, DateTimeOffset? since)
    {
        var found = new List<string>();
        foreach (var root in roots.Distinct(StringComparer.Ordinal).Where(Directory.Exists))
        {
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

    /// <summary>The lines of <paramref name="path"/> after byte <paramref name="offset"/> that contain <paramref name="marker"/>; a last line without its newline is left for the next call.</summary>
    /// <param name="path">The file.</param>
    /// <param name="offset">The byte just past the last complete line already read.</param>
    /// <param name="marker">A cheap pre-filter: lines without it are not returned.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The lines, the new offset, and whether the file got smaller than <paramref name="offset"/> and was read again from 0.</returns>
    public static async ValueTask<(IReadOnlyList<string> Lines, long Offset, bool Restarted)> LinesFrom(string path, long offset, string marker, CancellationToken cancellationToken)
    {
        var lines = new List<string>();
        var restarted = false;
        try
        {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize: 1, useAsync: true);
            await using var closing = stream.ConfigureAwait(false);
            if (stream.Length < offset)
            {
                offset = 0;
                restarted = true;
            }

            _ = stream.Seek(offset, SeekOrigin.Begin);
            var buffer = new byte[1 << 20];
            var line = new ArrayBufferWriter<byte>();
            int read;
            while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                offset = Split(buffer.AsSpan(0, read), line, offset, marker, lines);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // a transcript that vanished while it was read: what was read stands
        }

        return (lines, offset, restarted);
    }

    /// <summary>Cuts one chunk into lines; the bytes after the last newline wait in <paramref name="line"/> for the next chunk.</summary>
    /// <returns>The offset just past the last complete line.</returns>
    private static long Split(ReadOnlySpan<byte> chunk, ArrayBufferWriter<byte> line, long offset, string marker, List<string> lines)
    {
        while (!chunk.IsEmpty)
        {
            var newline = chunk.IndexOf((byte)'\n');
            if (newline < 0)
            {
                line.Write(chunk);
                break;
            }

            line.Write(chunk[..newline]);
            chunk = chunk[(newline + 1)..];
            offset += line.WrittenCount + 1;
            var text = Encoding.UTF8.GetString(line.WrittenSpan).TrimEnd('\r');
            line.Clear();
            if (text.Contains(marker, StringComparison.Ordinal))
            {
                lines.Add(text);
            }
        }

        return offset;
    }
}

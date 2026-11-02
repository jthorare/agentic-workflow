using System.Text;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>The last lines of a transcript or log, best effort (sandbox spec §3.6). Session end calls it; the wiring is SB-5a's.</summary>
internal static class TranscriptTail
{
    /// <summary>The default tail: 256 KiB.</summary>
    internal const int DefaultBytes = 256 * 1024;

    /// <summary>Reads the last bytes of a file as lines. A missing or unreadable file gives no lines; a cut first line is dropped.</summary>
    /// <param name="path">The file.</param>
    /// <param name="maxBytes">How much of the end to read.</param>
    /// <returns>The lines, oldest first.</returns>
    internal static IReadOnlyList<string> Read(string path, int maxBytes = DefaultBytes)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var length = stream.Length;
            var start = Math.Max(0, length - maxBytes);
            _ = stream.Seek(start, SeekOrigin.Begin);
            var buffer = new byte[length - start];
            var read = 0;
            while (read < buffer.Length)
            {
                var n = stream.Read(buffer, read, buffer.Length - read);
                if (n == 0)
                {
                    break;
                }

                read += n;
            }

            var lines = Encoding.UTF8.GetString(buffer, 0, read).Split('\n').Select(static l => l.TrimEnd('\r')).ToList();
            if (start > 0 && lines.Count > 0)
            {
                lines.RemoveAt(0);
            }

            return [.. lines.Where(static l => l.Length > 0)];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return [];
        }
    }
}

using System.Text;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>Reads the end of a transcript file that another process is appending to.</summary>
internal static class TailFile
{
    internal static async ValueTask<TranscriptTail> Read(string path, int maxBytes, CancellationToken cancellationToken)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize: 4096, useAsync: true);
        await using (stream.ConfigureAwait(false))
        {
            var start = Math.Max(0, stream.Length - maxBytes);
            _ = stream.Seek(start, SeekOrigin.Begin);
            var bytes = new byte[stream.Length - start];
            await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
            var lines = Encoding.UTF8.GetString(bytes).Split('\n').Select(static l => l.TrimEnd('\r')).ToList();
            if (start > 0 && lines.Count > 0)
            {
                lines.RemoveAt(0);   // cut by the start of the window
            }

            return new TranscriptTail(path, new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero), [.. lines.Where(static l => l.Length > 0)]);
        }
    }
}

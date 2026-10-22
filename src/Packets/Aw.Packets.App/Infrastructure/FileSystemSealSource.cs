using Aw.Kernel;
using Aw.Packets.Core;
using Aw.Packets.Core.Values;

namespace Aw.Packets.App.Infrastructure;

/// <summary>Reads a seal file from disk (a regular file; nothing else is read) and parses it with <see cref="SealReader"/>.</summary>
internal sealed class FileSystemSealSource : ISealSource
{
    /// <summary>The largest seal file read: it is four short fields, so anything bigger is not one.</summary>
    internal const int MaxBytes = 16 * 1024;

    public async ValueTask<Result<PacketHash>> Read(string path, CancellationToken cancellationToken)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return PacketsErrors.SealUnusable.With($"No seal file at {path}.");
            }

            // bounded read: a file that isn't a regular one (a pipe, a device) can report no length and never end
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, useAsync: true);
            await using (stream.ConfigureAwait(false))
            {
                var buffer = new byte[MaxBytes + 1];
                var read = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
                return read > MaxBytes
                    ? PacketsErrors.SealUnusable.With($"{path} is larger than {MaxBytes} bytes; a seal holds four short fields.")
                    : SealReader.Parse(path, buffer.AsMemory(0, read));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return PacketsErrors.SealUnusable.With($"The seal file {path} can't be read: {ex.Message}");
        }
    }
}

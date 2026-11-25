using System.Text;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Testing;

/// <summary>The in-memory ITranscriptReader: the one fake, kept honest by TranscriptReaderContract.</summary>
public sealed class InMemoryTranscriptReader : ITranscriptReader
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    /// <summary>Sets a transcript's whole content.</summary>
    /// <param name="path">The transcript path.</param>
    /// <param name="content">The text.</param>
    public void Write(string path, string content) => _files[path] = Encoding.UTF8.GetBytes(content);

    /// <summary>Appends text to a transcript.</summary>
    /// <param name="path">The transcript path.</param>
    /// <param name="content">The text.</param>
    public void Append(string path, string content) =>
        _files[path] = [.. _files.GetValueOrDefault(path, []), .. Encoding.UTF8.GetBytes(content)];

    /// <summary>The paths of the transcripts that exist.</summary>
    public IReadOnlyCollection<string> Paths => _files.Keys;

    /// <inheritdoc />
    public ValueTask<Result<TranscriptChunk>> ReadFrom(string path, long offset, CancellationToken cancellationToken)
    {
        if (!_files.TryGetValue(path, out var bytes))
        {
            return Done.Failed<TranscriptChunk>(MetaErrors.TranscriptUnreadable.With($"no transcript {path}"));
        }

        var start = offset > bytes.Length || offset < 0 ? 0 : (int)offset;
        var lines = new List<string>();
        var next = start;
        for (var at = start; at < bytes.Length; at++)
        {
            if (bytes[at] != (byte)'\n')
            {
                continue;
            }

            lines.Add(Encoding.UTF8.GetString(bytes, next, at - next).TrimEnd('\r'));
            next = at + 1;
        }

        return Done.Of(new TranscriptChunk(lines, next));
    }
}

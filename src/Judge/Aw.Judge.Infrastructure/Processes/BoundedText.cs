using System.Text;

namespace Aw.Judge.Infrastructure.Processes;

/// <summary>Reads a stream to its end in the background, keeping the first characters up to a limit and dropping the rest.</summary>
internal sealed class BoundedText
{
    private readonly StringBuilder _text = new();
    private readonly Lock _gate = new();
    private readonly int _limit;

    private BoundedText(int limit)
    {
        _limit = limit;
    }

    /// <summary>Finishes when the stream ends (or can't be read any more); never faults.</summary>
    public Task Reading { get; private set; } = Task.CompletedTask;

    /// <summary>What has been kept so far.</summary>
    public string Text
    {
        get
        {
            lock (_gate)
            {
                return _text.ToString();
            }
        }
    }

    /// <summary>Starts reading.</summary>
    /// <param name="reader">The stream.</param>
    /// <param name="limit">How many characters to keep.</param>
    /// <returns>The reader's text.</returns>
    public static BoundedText Read(StreamReader reader, int limit)
    {
        var text = new BoundedText(limit);
        text.Reading = text.ReadAll(reader);
        return text;
    }

    private async Task ReadAll(StreamReader reader)
    {
        var buffer = new char[8192];
        try
        {
            int read;
            while ((read = await reader.ReadAsync(buffer, CancellationToken.None).ConfigureAwait(false)) > 0)
            {
                Keep(buffer.AsSpan(0, read));
            }
        }
        catch (IOException)
        {
            // The pipe broke: what was read is the output.
        }
        catch (ObjectDisposedException)
        {
            // The run ended and the process was disposed while a child still held the pipe.
        }
    }

    private void Keep(ReadOnlySpan<char> chunk)
    {
        lock (_gate)
        {
            var room = _limit - _text.Length;
            if (room > 0)
            {
                _ = _text.Append(chunk[..Math.Min(room, chunk.Length)]);
            }
        }
    }
}

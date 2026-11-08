using System.Text;

namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>The size limits of a node run's stored text.</summary>
public static class NodeRunLimits
{
    /// <summary>A node's input and its output are each stored up to 64 KB (UTF-8 bytes); the rest is cut and 'Truncated' is set.</summary>
    public const int PayloadBytes = 64 * 1024;

    /// <summary>Cuts <paramref name="text"/> to <see cref="PayloadBytes"/> UTF-8 bytes, never in the middle of a character.</summary>
    /// <param name="text">The text, or null.</param>
    /// <param name="truncated">True when text was cut.</param>
    /// <returns>The text that fits.</returns>
    public static string? Cap(string? text, out bool truncated)
    {
        truncated = false;
        if (text is null || Encoding.UTF8.GetByteCount(text) <= PayloadBytes)
        {
            return text;
        }

        truncated = true;
        var length = 0;
        var bytes = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            bytes += rune.Utf8SequenceLength;
            if (bytes > PayloadBytes)
            {
                break;
            }

            length += rune.Utf16SequenceLength;
        }

        return text[..length];
    }
}

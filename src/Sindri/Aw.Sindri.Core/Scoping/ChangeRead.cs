using System.Text;

namespace Aw.Sindri.Core.Scoping;

/// <summary>How much of a change to read (scoping spec §4: the diff is at most 16 MiB).</summary>
/// <param name="MaxDiffBytes">The most diff text, in UTF-8 bytes.</param>
public sealed record ChangeRead(int MaxDiffBytes)
{
    /// <summary>The cap the scoping station reads with.</summary>
    public static ChangeRead Scoping { get; } = new(16 * 1024 * 1024);

    /// <summary>The start of <paramref name="text"/> that fits the cap, cut at a character boundary.</summary>
    /// <param name="text">The diff text.</param>
    /// <returns>The text that fits, and whether anything was cut.</returns>
    public (string Text, bool Truncated) Cut(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var bytes = Encoding.UTF8.GetBytes(text);
        if (bytes.Length <= MaxDiffBytes)
        {
            return (text, false);
        }

        var end = MaxDiffBytes;
        while (end > 0 && (bytes[end] & 0xC0) == 0x80)
        {
            end--;   // back up to the start of a character
        }

        return (Encoding.UTF8.GetString(bytes, 0, end), true);
    }
}

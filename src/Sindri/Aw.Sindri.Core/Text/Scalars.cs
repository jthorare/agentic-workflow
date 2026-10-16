using System.Text;

namespace Aw.Sindri.Core.Text;

/// <summary>Counts text the way the API spec's character limits do: in Unicode scalars, not UTF-16 units (P4 plan G2).</summary>
internal static class Scalars
{
    /// <summary>The number of Unicode scalars in <paramref name="text"/> (a lone surrogate counts as one).</summary>
    /// <param name="text">The text.</param>
    /// <returns>The count.</returns>
    public static int Count(string text)
    {
        var count = 0;
        var at = 0;
        while (at < text.Length)
        {
            var status = Rune.DecodeFromUtf16(text.AsSpan(at), out _, out var consumed);
            at += status == System.Buffers.OperationStatus.Done ? consumed : 1;
            count++;
        }

        return count;
    }
}

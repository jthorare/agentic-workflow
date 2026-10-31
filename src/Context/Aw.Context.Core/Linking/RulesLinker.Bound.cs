namespace Aw.Context.Core.Linking;

public static partial class RulesLinker
{
    // why: every pattern runs under a fixed 1 s timeout and one scan is linear, so an unbounded record (a ~20 MB run) would time out; ~1 MB keeps each scan to ~45 ms.
    internal const int MaxLinkedChars = 1_000_000;

    // why: an edge is looked for only near the limit, so one early newline in a long log or blob doesn't throw away nearly the whole window.
    internal const int EdgeWindow = 4_096;

    /// <summary>The prefix of <paramref name="text"/> that is linked: the text itself up to <see cref="MaxLinkedChars"/>, else the part up to the last newline (or, with none, the last whitespace) in the final <see cref="EdgeWindow"/> chars before the limit, else the limit itself, never splitting a surrogate pair.</summary>
    internal static string Bounded(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length <= MaxLinkedChars)
        {
            return text;
        }

        var head = text.AsSpan(0, MaxLinkedChars);
        var start = MaxLinkedChars - EdgeWindow;
        var edge = head[start..];
        var cut = edge.LastIndexOf('\n');
        if (cut < 0)
        {
            cut = LastWhiteSpace(edge);
        }

        if (cut >= 0)
        {
            return text[..(start + cut)];
        }

        return char.IsHighSurrogate(head[^1]) ? text[..(MaxLinkedChars - 1)] : text[..MaxLinkedChars];
    }

    private static int LastWhiteSpace(ReadOnlySpan<char> span)
    {
        for (var i = span.Length - 1; i >= 0; i--)
        {
            if (char.IsWhiteSpace(span[i]))
            {
                return i;
            }
        }

        return -1;
    }
}

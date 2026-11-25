using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// The text cuts of 'transcript_batch': a turn's text keeps its head, the assistant text before it keeps its tail, and each fits a size as JSON written twice, so a batch of
/// <see cref="TranscriptBatchBlock.MaxTurns"/> turns stays under the 64 KB node output cap whatever the script (a non-ASCII character is 6 bytes as JSON).
/// </summary>
internal static class TranscriptCut
{
    /// <summary>The most characters of a turn's text kept.</summary>
    public const int TextChars = 1500;

    /// <summary>The most bytes a turn's text takes as JSON written twice.</summary>
    public const int TextBytes = 2000;

    /// <summary>The most characters of the assistant text before a turn kept.</summary>
    public const int BeforeChars = 400;

    /// <summary>The most bytes that text takes as JSON written twice.</summary>
    public const int BeforeBytes = 500;

    /// <summary>The first <paramref name="chars"/> characters of <paramref name="text"/>, shortened until they take at most <paramref name="bytes"/> as JSON.</summary>
    /// <param name="text">The text.</param>
    /// <param name="chars">The most characters.</param>
    /// <param name="bytes">The most bytes as JSON.</param>
    /// <returns>The head; never a half surrogate pair at its end.</returns>
    public static string Head(string text, int chars, int bytes)
    {
        var cut = text.Length > chars ? text[..chars] : text;
        while (cut.Length > 0 && Size(cut) > bytes)
        {
            cut = cut[..(cut.Length * 3 / 4)];
        }

        return cut.Length > 0 && char.IsHighSurrogate(cut[^1]) ? cut[..^1] : cut;
    }

    /// <summary>The last <paramref name="chars"/> characters of <paramref name="text"/>, shortened from the front until they take at most <paramref name="bytes"/> as JSON.</summary>
    /// <param name="text">The text.</param>
    /// <param name="chars">The most characters.</param>
    /// <param name="bytes">The most bytes as JSON.</param>
    /// <returns>The tail; never a half surrogate pair at its start.</returns>
    public static string Tail(string text, int chars, int bytes)
    {
        var cut = text.Length > chars ? text[^chars..] : text;
        while (cut.Length > 0 && Size(cut) > bytes)
        {
            cut = cut[(cut.Length / 4)..];
        }

        return cut.Length > 0 && char.IsLowSurrogate(cut[0]) ? cut[1..] : cut;
    }

    // why: the engine writes JSON with the default writer, which escapes every non-ASCII character and every quote as 6 bytes; the turns then reach 'label'
    // inside its prompt text, which is stored as JSON again, so the size is counted as JSON written twice (the steering-miner 'shape' step counts the same way)
    private static int Size(string text) => JsonValue.Create(JsonValue.Create(text).ToJsonString()).ToJsonString().Length;
}

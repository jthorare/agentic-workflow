namespace Aw.Transcripts.Core.Prompts;

/// <summary>
/// Tells typed prompt text from text the harness writes into a user line: tag-led blocks (system reminders, hook output,
/// slash-command wrappers, teammate and task messages, environment context), compaction summaries, skill preambles,
/// peer-session deliveries and interrupt markers. The provider readers apply their own line rules first.
/// </summary>
public static class PromptText
{
    // Text the harness writes that does not start with a tag.
    private static readonly string[] _machinePrefixes =
    [
        "This session is being continued from a previous conversation",
        "Base directory for this skill",
        "Caveat:",
        "Another Claude session sent a message",
        "[Request interrupted by user",
        "[SYSTEM NOTIFICATION",
        "Stop hook feedback:",
        "# AGENTS.md instructions",
    ];

    // A tag-led prompt a person still typed: a paste the harness wrapped.
    private const string PastedContent = "<pasted_content";

    /// <summary>Whether a user line's text is what a person typed.</summary>
    /// <param name="text">The text of the line's text blocks.</param>
    /// <returns>False for empty text, tag-led harness text and the known machine prefixes.</returns>
    public static bool IsTyped(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var trimmed = text.TrimStart();
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (trimmed[0] == '<')
        {
            return trimmed.StartsWith(PastedContent, StringComparison.Ordinal);
        }

        return !Array.Exists(_machinePrefixes, prefix => trimmed.StartsWith(prefix, StringComparison.Ordinal));
    }

    /// <summary>The text after the leading '&lt;ide_opened_file&gt;' and '&lt;ide_selection&gt;' blocks an IDE extension puts in front of a typed prompt.</summary>
    /// <param name="text">The text of the line's text blocks.</param>
    /// <returns>The text without those blocks, start-trimmed.</returns>
    public static string WithoutIdeBlocks(string text) => throw new NotSupportedException("red: strip the IDE blocks");

    /// <summary>Strips the leading '&lt;tag …&gt;…&lt;/tag&gt;' blocks a host wraps around a prompt; a block that never closes stays.</summary>
    /// <param name="text">The text.</param>
    /// <returns>What follows the blocks, start-trimmed.</returns>
    public static string StripLeadingTagBlocks(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var rest = text.TrimStart();
        while (TagName(rest) is { } name)
        {
            var close = $"</{name}>";
            var end = rest.IndexOf(close, StringComparison.Ordinal);
            if (end < 0)
            {
                return rest;
            }

            rest = rest[(end + close.Length)..].TrimStart();
        }

        return rest;
    }

    // The name of the tag the text opens with, or null.
    private static string? TagName(string text)
    {
        if (text.Length < 3 || text[0] != '<' || !char.IsAsciiLetter(text[1]))
        {
            return null;
        }

        var end = 2;
        while (end < text.Length && (char.IsAsciiLetterOrDigit(text[end]) || text[end] is '_' or '-'))
        {
            end++;
        }

        return end < text.Length && text[end] is '>' or ' ' ? text[1..end] : null;
    }
}

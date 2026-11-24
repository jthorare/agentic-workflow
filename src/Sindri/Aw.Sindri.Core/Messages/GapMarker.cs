using System.Globalization;

namespace Aw.Sindri.Core.Messages;

/// <summary>A hole in a conversation's loaded lines (messages spec §5.3, W10): a conversation row of its own, so it scrolls with the messages.</summary>
/// <param name="Conversation">The conversation.</param>
/// <param name="After">The newest loaded ts before the hole.</param>
/// <param name="Before">The oldest loaded ts after the hole.</param>
/// <param name="Missing">How many messages are not loaded, when Slack says; 0 when unknown.</param>
public sealed record GapMarker(string Conversation, MessageTs After, MessageTs Before, int Missing)
{
    private const string LoadOlder = "Load older (1 per minute)";

    /// <summary>The marker's line: '12 older messages not loaded. Load older (1 per minute)'; with no count, 'Older messages not loaded. …'.</summary>
    /// <returns>The line.</returns>
    public string Line() => Missing switch
    {
        <= 0 => $"Older messages not loaded. {LoadOlder}",
        1 => $"1 older message not loaded. {LoadOlder}",
        _ => string.Create(CultureInfo.InvariantCulture, $"{Missing} older messages not loaded. {LoadOlder}"),
    };
}

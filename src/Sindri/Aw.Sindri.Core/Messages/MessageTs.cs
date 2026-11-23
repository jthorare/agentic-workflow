namespace Aw.Sindri.Core.Messages;

/// <summary>A Slack message timestamp ('1700000000.000100'): a message's key inside its conversation and its place in time (messages spec §8.1). M1b parses and orders it.</summary>
/// <param name="Value">The text Slack sends.</param>
public readonly record struct MessageTs(string Value)
{
    /// <summary>Reads a Slack ts: seconds, a dot and six digits; any other text is refused.</summary>
    /// <param name="text">The text Slack sent.</param>
    /// <returns>The ts.</returns>
    public static MessageTs Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        throw MessageStubs.NotBuilt("MessageTs.Parse", "M1b");
    }

    /// <summary>Orders two ts by time.</summary>
    /// <param name="left">One ts.</param>
    /// <param name="right">The other.</param>
    /// <returns>Negative when <paramref name="left"/> is older, zero when equal, positive when newer.</returns>
    public static int Compare(MessageTs left, MessageTs right)
    {
        _ = (left, right);
        throw MessageStubs.NotBuilt("MessageTs.Compare", "M1b");
    }
}

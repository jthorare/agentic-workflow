namespace Aw.Sindri.Core.Messages;

/// <summary>A row's sort key in Needs reply (messages spec §7.2): class high first, then blocks, then the newest message oldest first, then the conversation.</summary>
/// <param name="Class">The class.</param>
/// <param name="Blocks">What its 'about' links block.</param>
/// <param name="Newest">The newest message's ts.</param>
/// <param name="Conversation">The conversation, the last tie-break.</param>
public readonly record struct MessageRank(MessageClass Class, int Blocks, MessageTs Newest, string Conversation)
{
    /// <summary>The rank of a row.</summary>
    /// <param name="row">The row.</param>
    /// <returns>Its rank.</returns>
    public static MessageRank Of(ThreadRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        throw MessageStubs.NotBuilt("MessageRank.Of", "M1b");
    }

    /// <summary>The rows in Needs reply order.</summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The rows, first to answer first.</returns>
    public static IReadOnlyList<ThreadRow> Order(IEnumerable<ThreadRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        throw MessageStubs.NotBuilt("MessageRank.Order", "M1b");
    }
}

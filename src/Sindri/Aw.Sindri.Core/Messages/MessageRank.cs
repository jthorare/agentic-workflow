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
        return new MessageRank(row.Class, row.Blocks, row.Newest, row.Conversation);
    }

    /// <summary>Orders two ranks the Needs reply way: the higher class, then more blocks, then the one waiting longer, then the conversation id.</summary>
    /// <param name="left">One rank.</param>
    /// <param name="right">The other.</param>
    /// <returns>Negative when <paramref name="left"/> comes first, zero when they tie, positive when <paramref name="right"/> comes first.</returns>
    public static int Compare(MessageRank left, MessageRank right)
    {
        var byClass = right.Class.CompareTo(left.Class);
        if (byClass != 0)
        {
            return byClass;
        }

        var byBlocks = right.Blocks.CompareTo(left.Blocks);
        if (byBlocks != 0)
        {
            return byBlocks;
        }

        var byTime = MessageTs.Compare(left.Newest, right.Newest);
        return byTime != 0 ? byTime : string.CompareOrdinal(left.Conversation, right.Conversation);
    }

    /// <summary>The rows in Needs reply order. Two threads of one conversation that tie on every key keep a fixed order by row key.</summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The rows, first to answer first.</returns>
    public static IReadOnlyList<ThreadRow> Order(IEnumerable<ThreadRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var ranked = rows.Select(static row => (Row: row, Rank: Of(row))).ToList();
        ranked.Sort(static (left, right) =>
        {
            var byRank = Compare(left.Rank, right.Rank);
            return byRank != 0 ? byRank : string.CompareOrdinal(left.Row.Key, right.Row.Key);
        });
        return ranked.ConvertAll(static r => r.Row);
    }
}

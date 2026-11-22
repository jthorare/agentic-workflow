using Aw.Kernel.Testing;
using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Tests.Messages;

/// <summary>M1b (messages spec §7.2): each key of the Needs reply order, one at a time.</summary>
[Lane(TestLane.Unit)]
public sealed class MessageRankTests
{
    [Fact]
    public void A_higher_class_comes_first_whatever_its_blocks_and_age()
    {
        var fyi = Row("D0A", "1767700000.000100") with
        {
            Class = MessageClass.Fyi,
            Blocks = 9,
        };
        var question = Row("D0B", "1767799999.000100") with { Class = MessageClass.DirectQuestion };

        Assert.Equal(["D0B", "D0A"], Conversations(fyi, question), StringComparer.Ordinal);
    }

    [Fact]
    public void Within_a_class_more_blocks_come_first()
    {
        var none = Row("D0A", "1767700000.000100") with { Class = MessageClass.ReviewAsk };
        var two = Row("D0B", "1767799999.000100") with
        {
            Class = MessageClass.ReviewAsk,
            Blocks = 2,
        };

        Assert.Equal(["D0B", "D0A"], Conversations(none, two), StringComparer.Ordinal);
    }

    [Fact]
    public void Within_class_and_blocks_the_longest_waiting_comes_first()
    {
        var newer = Row("D0A", "1767776640.000100") with { Class = MessageClass.DirectQuestion };
        var older = Row("D0B", "1767776520.000100") with { Class = MessageClass.DirectQuestion };

        Assert.Equal(["D0B", "D0A"], Conversations(newer, older), StringComparer.Ordinal);
    }

    [Fact]
    public void A_full_tie_breaks_on_the_conversation_then_the_row_key()
    {
        var b = Row("C0B", "1767776100.000100", "slack:thread:T0/C0B/1.000000");
        var a2 = Row("C0A", "1767776100.000100", "slack:thread:T0/C0A/2.000000");
        var a1 = Row("C0A", "1767776100.000100", "slack:thread:T0/C0A/1.000000");

        Assert.Equal(["slack:thread:T0/C0A/1.000000", "slack:thread:T0/C0A/2.000000", "slack:thread:T0/C0B/1.000000"], MessageRank.Order([b, a2, a1]).Select(static r => r.Key), StringComparer.Ordinal);
    }

    [Fact]
    public void Rank_of_a_row_carries_its_keys()
    {
        var row = Row("D0A", "1767776100.000100") with
        {
            Class = MessageClass.ReviewAsk,
            Blocks = 3,
        };

        Assert.Equal(new MessageRank(MessageClass.ReviewAsk, 3, new MessageTs("1767776100.000100"), "D0A"), MessageRank.Of(row));
    }

    [Fact]
    public void Order_of_no_rows_is_empty() => Assert.Empty(MessageRank.Order([]));

    private static string[] Conversations(params ThreadRow[] rows) => [.. MessageRank.Order(rows).Select(static r => r.Conversation)];

    private static ThreadRow Row(string conversation, string newest, string? key = null) =>
        new(key ?? "slack:dm:T0/" + conversation, ConversationKind.Dm, conversation, new MessageTs(newest));
}

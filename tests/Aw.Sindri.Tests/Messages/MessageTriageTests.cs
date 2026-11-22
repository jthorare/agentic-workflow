using Aw.Kernel.Testing;
using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Tests.Messages;

/// <summary>M1b (messages spec §7.1, §7.2): the one definition of Needs reply, the class rules and the rank, over the worked example of §7.2.</summary>
[Lane(TestLane.Unit)]
public sealed class MessageTriageTests
{
    private static readonly IReadOnlyList<string> _phrases = ["can you", "could you", "please", "need you", "when can"];

    // §7.2's list at 09:10, plus Lee's DM at 09:12 (the New band, ranked by R at 09:16). All names and numbers are made up.
    private static ThreadRow Kim() => Row("slack:thread:T0HARBOR/C0HARBOR/1767771000.000100", ConversationKind.Channel, "C0HARBOR", "1767776100.000100") with
    {
        Thread = new MessageTs("1767771000.000100"),
        Mine = MineReason.NamedMe,
        NamesMe = true,
        NewestText = "@joi can you check the group booking path uses 90 s too?",
        LinksWaitingOnMe = true,
        Blocks = 2,
    };

    private static ThreadRow Sam() => Row("slack:dm:T0HARBOR/D0SAM", ConversationKind.Dm, "D0SAM", "1767776520.000100") with
    {
        Members = 2,
        Mine = MineReason.DirectMessage,
        NewestText = "can you ship the fare table today?",
    };

    private static ThreadRow Ada() => Row("slack:thread:T0HARBOR/C0HARBOR/1767748200.000100", ConversationKind.Channel, "C0HARBOR", "1767748200.000100") with
    {
        Thread = new MessageTs("1767748200.000100"),
        Mine = MineReason.WroteInThread,
        NewestText = "also #3125 is stacked on 3120, can you look at both",
        LinksWaitingOnMe = true,
    };

    private static ThreadRow Pat() => Row("slack:dm:T0HARBOR/D0PAT", ConversationKind.Dm, "D0PAT", "1767765600.000100") with
    {
        Members = 2,
        Mine = MineReason.DirectMessage,
        NewestText = "thanks!",
    };

    private static ThreadRow Lee() => Row("slack:dm:T0HARBOR/D0LEE", ConversationKind.Dm, "D0LEE", "1767776640.000100") with
    {
        Members = 2,
        Mine = MineReason.DirectMessage,
        NewestText = "got a sec?",
    };

    [Fact]
    public void MessageRank_orders_the_example() => RedUntil.Phase("M1b", () =>
    {
        var rows = new[] { Pat(), Ada(), Sam(), Kim(), Lee() }.Select(r => r with { Class = MessageTriage.Classify(r, _phrases) });

        var order = MessageRank.Order(rows).Select(static r => r.Conversation + (r.Thread is { } t ? "/" + t.Value : string.Empty));

        // Kim's thread blocks 2, so it leads class 3; Lee's 09:12 DM takes its ranked place under it after R; then Sam, Ada (class 2), Pat (class 1)
        Assert.Equal(["C0HARBOR/1767771000.000100", "D0LEE", "D0SAM", "C0HARBOR/1767748200.000100", "D0PAT"], order, StringComparer.Ordinal);
    });

    [Fact]
    public void Needs_reply_matches_the_one_definition() => RedUntil.Phase("M1b", () =>
    {
        // §7.1: mine, the newest message not mine, no reply of mine after it, and not Done, snoozed or linked to an issue with an owner
        (string Why, ThreadRow Row, bool Expected)[] cases =
        [
            ("mine, newest not mine, no reply after it", Sam(), true),
            ("not mine", Sam() with { Mine = null }, false),
            ("my message is the newest", Sam() with { NewestIsMine = true }, false),
            ("I replied after it", Sam() with { RepliedAfterNewest = true }, false),
            ("Done", Sam() with { Done = true }, false),
            ("snoozed", Sam() with { Snoozed = true }, false),
            ("linked to an issue with an owner", Sam() with { LinkedToOwnedIssue = true }, false),
            ("a read mark never drops it", Sam(), true),
        ];

        Assert.All(cases, c => Assert.True(c.Expected == MessageTriage.NeedsReply(c.Row), c.Why));
    });

    [Fact]
    public void Release_1_needs_reply_holds_class_3_and_2_only() => RedUntil.Phase("M1b", () =>
    {
        var rows = new[] { Kim(), Sam(), Ada(), Pat() }.Select(r => r with { Class = MessageTriage.Classify(r, _phrases) }).ToList();

        Assert.Equal([MessageClass.DirectQuestion, MessageClass.DirectQuestion, MessageClass.ReviewAsk, MessageClass.Fyi], rows.Select(static r => r.Class));
        Assert.Equal(["C0HARBOR", "D0SAM", "C0HARBOR"], rows.Where(MessageTriage.InRelease1NeedsReply).Select(static r => r.Conversation), StringComparer.Ordinal);   // Pat (class 1) shows in Unread (call 21)
    });

    private static ThreadRow Row(string key, ConversationKind kind, string conversation, string newest) => new(key, kind, conversation, new MessageTs(newest));
}

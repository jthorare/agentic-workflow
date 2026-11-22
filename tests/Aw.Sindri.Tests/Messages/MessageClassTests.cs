using Aw.Kernel.Testing;
using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Tests.Messages;

/// <summary>M1b (messages spec §7.2): the class rules past the worked example: who the message is for, and what counts as a question.</summary>
[Lane(TestLane.Unit)]
public sealed class MessageClassTests
{
    private static readonly IReadOnlyList<string> _phrases = ["can you", "could you", "please", "need you", "when can"];

    [Theory]
    [InlineData("please ship the fare table")]
    [InlineData("Could You look at 3120")]
    [InlineData("when can we pair")]
    public void A_request_phrase_without_a_question_mark_is_a_direct_question(string text) =>
        Assert.Equal(MessageClass.DirectQuestion, Classify(Dm(text)));

    [Theory]
    [InlineData("pleased with how it went")]
    [InlineData("the uncan youth club")]
    [InlineData("see <https://harbor.example/fares?page=2> for the fares")]
    [InlineData("the flag is `--dry-run?` in the docs")]
    [InlineData("```\nwhy?\n```")]
    [InlineData("nice :question:")]
    public void Text_that_only_looks_like_a_question_is_fyi(string text) =>
        Assert.Equal(MessageClass.Fyi, Classify(Dm(text)));

    [Fact]
    public void A_link_label_with_a_question_mark_is_a_question() =>
        Assert.Equal(MessageClass.DirectQuestion, Classify(Dm("<https://harbor.example/3120|ready to merge?>")));

    [Fact]
    public void A_question_in_a_channel_that_names_no_one_is_not_a_direct_question()
    {
        var row = Channel("anyone know why the build is red?");

        Assert.Equal(MessageClass.Fyi, Classify(row));
        Assert.Equal(MessageClass.ReviewAsk, Classify(row with { LinksWaitingOnMe = true }));
        Assert.Equal(MessageClass.DirectQuestion, Classify(row with { NamesMe = true }));
    }

    [Theory]
    [InlineData(3, MessageClass.DirectQuestion)]
    [InlineData(4, MessageClass.Fyi)]
    [InlineData(0, MessageClass.Fyi)]
    public void A_group_dm_counts_as_small_up_to_3_people(int members, MessageClass expected)
    {
        var group = Dm("can you check?") with
        {
            Kind = ConversationKind.Mpim,
            Members = members,
        };

        Assert.Equal(expected, Classify(group));
    }

    [Fact]
    public void A_thread_that_is_not_mine_or_has_my_last_word_has_no_class()
    {
        Assert.Equal(MessageClass.None, Classify(Dm("can you check?") with { Mine = null }));
        Assert.Equal(MessageClass.None, Classify(Dm("can you check?") with { NewestIsMine = true }));
    }

    [Fact]
    public void A_blank_request_phrase_matches_nothing() =>
        Assert.Equal(MessageClass.Fyi, MessageTriage.Classify(Dm("ok, thanks"), ["", "  "]));

    [Fact]
    public void Release_1_needs_reply_leaves_out_a_row_that_needs_no_reply()
    {
        var done = Dm("can you check?") with
        {
            Class = MessageClass.DirectQuestion,
            Done = true,
        };

        Assert.False(MessageTriage.InRelease1NeedsReply(done));
    }

    private static MessageClass Classify(ThreadRow row) => MessageTriage.Classify(row, _phrases);

    private static ThreadRow Dm(string text) =>
        new ThreadRow("slack:dm:T0HARBOR/D0SAM", ConversationKind.Dm, "D0SAM", new MessageTs("1767776520.000100")) with
        {
            Members = 2,
            Mine = MineReason.DirectMessage,
            NewestText = text,
        };

    private static ThreadRow Channel(string text) =>
        new ThreadRow("slack:thread:T0HARBOR/C0HARBOR/1767748200.000100", ConversationKind.Channel, "C0HARBOR", new MessageTs("1767748200.000100")) with
        {
            Thread = new MessageTs("1767748200.000100"),
            Mine = MineReason.WroteInThread,
            NewestText = text,
        };
}

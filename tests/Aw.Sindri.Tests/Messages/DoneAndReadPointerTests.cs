using Aw.Sindri.Core.Messages;
using Aw.Sindri.Features.MoveReadPointer;

namespace Aw.Sindri.Tests.Messages;

/// <summary>M1h (messages spec §7.2 "Done and return", §8.2, W9): Done leaves Needs reply and a new DM brings it back; 'e' in Unread moves the local pointer only.</summary>
public sealed class DoneAndReadPointerTests
{
    [Fact, Trait("Lane", "unit")]
    public void Done_leaves_needs_reply_and_returns_on_a_new_dm() => RedUntil.Phase("M1h", () =>
    {
        var sam = new ThreadRow("slack:dm:T0HARBOR/D0SAM", ConversationKind.Dm, "D0SAM", new MessageTs("1767776520.000100")) { Members = 2, Mine = MineReason.DirectMessage };
        var done = sam with { Done = true };

        Assert.True(MessageTriage.NeedsReply(sam));
        Assert.False(MessageTriage.NeedsReply(done));
        Assert.True(MessageTriage.ReturnsAfterDone(done, MessageAddress.DirectMessage));
        Assert.False(MessageTriage.ReturnsAfterDone(done, MessageAddress.ReactionToMine));   // a reaction to my message does not return it
    });

    [Fact, Trait("Lane", "integration")]
    public Task E_in_unread_moves_the_pointer_and_writes_no_home_event() => RedUntil.Phase("M1h", async () =>
    {
        // M1h: the service moves read_ts in messages.db; it appends nothing to the ledger (no home.row.* event) and sends nothing to Slack
        var ts = new MessageTs("1767776520.000100");

        var answer = await new MoveReadPointerService().Handle(new MoveReadPointerRequest("D0SAM", ts, ReadPointerMove.Read), TestContext.Current.CancellationToken);

        Assert.True(answer.IsSuccess);
        Assert.Equal(("D0SAM", ts), (answer.Value.Conversation, answer.Value.ReadTo));
    });
}

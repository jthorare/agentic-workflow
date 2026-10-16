using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Tests.Views;

/// <summary>The sentences of the P4 lists (API spec §3.6; UI screens R8-13): zeroes shown, no 'waiting on you'.</summary>
[Lane(TestLane.Unit)]
public sealed class EmptyTextRequestsTests
{
    private static readonly ItemId _item = ItemId.From("ITM-" + new string('1', 32));

    [Fact]
    public void Needs_you_empty_sentence_counts_running_and_queued()
    {
        Assert.Equal("Nothing needs you. 0 running, 0 queued.", EmptyText.NeedsYou(running: 0, queued: 0));
        Assert.Equal("Nothing needs you. 3 running, 12 queued.", EmptyText.NeedsYou(running: 3, queued: 12));
    }

    [Fact]
    public void Decision_sentences_follow_the_filters()
    {
        Assert.Equal("No decisions are waiting.", EmptyText.Decisions(pending: true, item: null));
        Assert.Equal("Nothing decided yet.", EmptyText.Decisions(pending: false, item: null));
        Assert.Equal("No decisions.", EmptyText.Decisions(pending: null, item: null));
        Assert.Equal($"No decisions for {_item.Value}.", EmptyText.Decisions(pending: true, _item));
    }

    [Fact]
    public void Request_sentences_follow_the_filters()
    {
        Assert.Equal("No requests filed.", EmptyText.Requests(state: null, item: null));
        Assert.Equal("No open requests.", EmptyText.Requests(RequestState.Open, item: null));
        Assert.Equal("No declined requests.", EmptyText.Requests(RequestState.Declined, item: null));
        Assert.Equal($"No requests for {_item.Value}.", EmptyText.Requests(state: null, _item));
    }

    [Fact]
    public void The_two_notices_are_the_API_spec_wording()
    {
        Assert.Equal("recorded; acted on once the workflow engine runs", EmptyText.RecordedOnly());
        Assert.Equal("voided: the item was closed", EmptyText.Voided());
    }
}

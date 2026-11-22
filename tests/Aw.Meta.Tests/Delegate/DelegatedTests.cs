using Aw.Meta.Core.Delegations;
using Aw.Meta.Features.Delegate;
using Aw.Meta.Features.Delegated;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Delegate;

public sealed class DelegatedTests
{
    private readonly DelegateWorld _d = new();

    private async Task<IReadOnlyList<DelegationView>> List(bool all = false) =>
        (await _d.Listing().Handle(new DelegatedRequest(all), CancellationToken.None)).Value.Delegations;

    private async Task Inbound(string id, string from, string body, int minute) =>
        _ = await _d.World.Messages.RecordInbound("/transcripts/other.jsonl", 0, [A.Inbox(id, from, body, minute)], CancellationToken.None);

    private async Task Delegated(int issue, string lane)
    {
        var n = issue.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _ = _d.World.Runner.When(
            "issue view " + n,
            $$"""{"number":{{n}},"title":"Task {{n}}","state":"OPEN","url":"https://github.com/o/r/issues/{{n}}"}""");
        _ = await _d.Delegate(new DelegateRequest(lane, Title: null, What: null, Issue: issue));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Owed_sent_and_answered_follow_the_messages_and_answered_shows_only_with_all()
    {
        await _d.Ready();
        await Delegated(10, "api");   // owed: nothing sent
        await Delegated(11, "ui");    // sent: a SendMessage naming #11
        await Delegated(12, "api");   // answered: the lane wrote back naming #12
        _ = await _d.World.Messages.RecordOutbound(A.Sent("t1-ui", "ACTION: #11 Task 11", minute: 61), CancellationToken.None);
        await Inbound("m1", "t1-api", "on #12, PR #900 soon", minute: 62);
        await Inbound("m2", "t1-ui", "about #10: not mine", minute: 62);   // another lane naming #10 is no answer
        await Inbound("m3", "t1-api", "earlier #10", minute: 30);           // before the delegation

        var open = await List();
        var all = await List(all: true);

        Assert.Equal([(10, "owed"), (11, "sent")], open.Select(static d => (d.Issue, d.State)));
        Assert.Equal([(10, "owed"), (11, "sent"), (12, "answered")], all.Select(static d => (d.Issue, d.State)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Nothing_delegated_lists_nothing()
    {
        await _d.Ready();

        Assert.Empty(await List(all: true));
    }
}

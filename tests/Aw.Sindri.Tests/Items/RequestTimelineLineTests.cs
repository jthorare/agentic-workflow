using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.Items;

/// <summary>The timeline lines of the two P4 events (API spec §3.4; P4 plan 1.4).</summary>
[Lane(TestLane.Unit)]
public sealed class RequestTimelineLineTests
{
    private static readonly RequestId _request = RequestId.From("REQ-" + new string('1', 32));
    private static readonly DecisionId _decision = DecisionId.From("DEC-" + new string('2', 32));
    private readonly SliceHarness _h = new();

    [Theory]
    [InlineData(RequestKind.Rescope, "re-scope")]
    [InlineData(RequestKind.AddTask, "add task")]
    [InlineData(RequestKind.Note, "note")]
    [InlineData(RequestKind.Handback, "hand back")]
    public void A_filed_request_says_its_kind_and_who_filed_it(RequestKind kind, string words)
    {
        var filed = new RequestFiled(_h.Ids.New<CommandId>(), _request, _decision, kind, Item: null, Repo: null, "text");
        var recorded = Recorded.Of(_h.Events, EventTypes.RequestFiled, filed, 1);

        Assert.Equal($"{words} request filed by {recorded.Envelope.Actor}", TimelineLine.For(recorded, SliceHarness.Json));
    }

    [Theory]
    [InlineData("accept", "accepted")]
    [InlineData("decline", "declined")]
    [InlineData("defer", "chose defer")]
    public void A_recorded_decision_says_the_choice_and_who_made_it(string choice, string words)
    {
        var decided = new DecisionRecorded(_h.Ids.New<CommandId>(), _decision, DecisionOption.From(choice), Note: null, "human:prover");
        var recorded = Recorded.Of(_h.Events, EventTypes.DecisionRecorded, decided, 2);

        Assert.Equal($"decision {_decision.Value} {words} by human:prover", TimelineLine.For(recorded, SliceHarness.Json));
    }
}

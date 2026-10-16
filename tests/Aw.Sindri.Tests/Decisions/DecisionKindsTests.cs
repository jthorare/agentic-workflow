using Aw.Kernel.Testing;
using Aw.Sindri.Core.Decisions;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Decisions;

/// <summary>The options a decision offers and its labels (P4 plan §1.2; UI spec E-7; owner decision of 2026-10-14 on X).</summary>
[Lane(TestLane.Unit)]
public sealed class DecisionKindsTests
{
    [Fact]
    public void Accept_request_offers_accept_then_decline_and_only_decline_is_reject_like()
    {
        var options = DecisionKinds.Options(DecisionKind.AcceptRequest);

        Expect.Sequence(
            [new DecisionOptionSpec(DecisionOption.From("accept"), "Accept", RejectLike: false), new DecisionOptionSpec(DecisionOption.From("decline"), "Decline", RejectLike: true)],
            options);
        Expect.Equal(DecisionOption.From("accept"), DecisionKinds.Recommended(DecisionKind.AcceptRequest));
    }

    [Fact]
    public void The_recommended_option_is_one_of_the_options()
    {
        foreach (var kind in Enum.GetValues<DecisionKind>())
        {
            Expect.True(DecisionKinds.Options(kind).Any(o => o.Id == DecisionKinds.Recommended(kind)));
        }
    }

    [Theory]
    [InlineData(RequestKind.Rescope, "decision · re-scope")]
    [InlineData(RequestKind.AddTask, "decision · add task")]
    [InlineData(RequestKind.Note, "decision · note")]
    [InlineData(RequestKind.Handback, "decision · hand back")]
    public void The_label_names_the_request_kind(RequestKind kind, string label) => Expect.Equal(label, DecisionLabels.For(DecisionKind.AcceptRequest, kind));
}

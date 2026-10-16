using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.NeedsYou;

/// <summary>The order of the human's list: urgency, then since, then id (P4 plan §1.3).</summary>
[Lane(TestLane.Unit)]
public sealed class NeedsYouOrderTests
{
    private static readonly DateTimeOffset _t = KnownInstants.Epoch;

    [Fact]
    public void Needs_you_orders_by_urgency_then_since_then_id()
    {
        var calm = Row("decision:DEC-" + new string('1', 32), Urgency.Calm, _t);
        var colourLate = Row("decision:DEC-" + new string('2', 32), Urgency.Colour, _t.AddMinutes(5));
        var colourEarly = Row("decision:DEC-" + new string('3', 32), Urgency.Colour, _t);
        var pulseB = Row("decision:DEC-" + new string('b', 32), Urgency.Pulse, _t.AddHours(1));
        var pulseA = Row("decision:DEC-" + new string('a', 32), Urgency.Pulse, _t.AddHours(1));

        var sorted = NeedsYouOrder.Sort([calm, colourLate, pulseB, colourEarly, pulseA]);

        Expect.Sequence([pulseA, pulseB, colourEarly, colourLate, calm], sorted);
    }

    [Fact]
    public void Compare_is_zero_for_the_same_row_and_antisymmetric()
    {
        var a = Row("decision:DEC-" + new string('a', 32), Urgency.Pulse, _t);
        var b = Row("decision:DEC-" + new string('b', 32), Urgency.Pulse, _t);

        Expect.Equal(0, NeedsYouOrder.Compare(a, a));
        Expect.True(NeedsYouOrder.Compare(a, b) < 0 && NeedsYouOrder.Compare(b, a) > 0);
    }

    private static NeedsYouRow Row(string id, Urgency urgency, DateTimeOffset since) =>
        A.NeedsYouRow().Urgent(urgency, since).Build() with { Id = NeedsYouRowId.From(id) };
}

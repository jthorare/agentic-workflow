using Aw.Context.Core.Packing;
using Aw.Context.Core.Results;

namespace Aw.Context.Tests.Core;

public sealed class PackerTests
{
    private static PackCandidate Candidate(string section, string marker, int tokens) => new(section,
        new ResultItem("record", Node: null, marker, "text", Score: null, [], Provenance.Deterministic, Confirmed: false, Trust.Untrusted, [marker]) { Tokens = tokens },
        marker);

    [Fact, Trait("Lane", "unit")]
    public void Sections_come_in_fill_order_and_every_item_is_kept_in_the_given_order()
    {
        var packed = Packer.Pack([Candidate("records", "C1", 40), Candidate("graph", "C2", 50), Candidate("records", "C3", 30), Candidate("records", "C4", 10)]);

        Assert.Equal(["graph", "records"], packed.Sections.Select(s => s.Name), StringComparer.Ordinal);
        Assert.Equal(["C1", "C3", "C4"], packed.Sections[1].Items.Select(i => i.Title ?? ""), StringComparer.Ordinal);
        Assert.Equal(130, packed.Used);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_big_result_set_is_never_cut_by_size()
    {
        var candidates = Enumerable.Range(0, 500).Select(i => Candidate("graph", "G" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), tokens: 5_000)).ToList();

        var packed = Packer.Pack(candidates);

        Assert.Equal(500, packed.Sections.Single().Items.Count);
        Assert.Equal(2_500_000, packed.Used);
    }

    [Fact, Trait("Lane", "unit")]
    public void Tokens_are_utf8_bytes_over_four_rounded_up()
    {
        Assert.Equal(0, TokenEstimate.Of(text: null));
        Assert.Equal(1, TokenEstimate.Of("abc"));
        Assert.Equal(2, TokenEstimate.Of("åäö"));   // 6 bytes
    }
}

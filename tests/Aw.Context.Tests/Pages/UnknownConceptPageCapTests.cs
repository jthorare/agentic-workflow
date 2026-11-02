using Aw.Context.Core.Engine;
using Aw.Context.Core.Pages;

namespace Aw.Context.Tests.Pages;

/// <summary>A row whose only problem is an unknown concept still adds a page, so it counts against the 200-page cap in block order.</summary>
public sealed class UnknownConceptPageCapTests
{
    private const string CapReason = "Not added: The profile holds at most 200 pages";
    private const string UnknownReason = "No concept called \"aproval\": pick one";

    private static readonly (ConceptId Id, string Name)[] _concepts = [(ConceptId.From("approval-workflow"), "Approval workflow")];

    private static IReadOnlyList<PageListRow> Preview(string paste, int ownerPages) =>
        PageListPreview.Of(PageListText.Parse(paste), [], _concepts, ownerPages);

    [Fact, Trait("Lane", "unit")]
    public void With_200_owner_pages_an_unknown_concept_block_gets_the_cap_reason_and_no_page()
    {
        var row = Preview("Home\nPay -> aproval", ownerPages: 200)[0];

        Assert.False(row.Ticked);
        Assert.Equal(CapReason, row.Reason);
        Assert.Null(row.Page);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_concept_block_takes_the_last_slot_before_a_later_new_page()
    {
        var rows = Preview("Home\nPay -> aproval\n\nBilling", ownerPages: 199);

        Assert.False(rows[0].Ticked);
        Assert.Equal(UnknownReason, rows[0].Reason);
        Assert.NotNull(rows[0].Page);
        Assert.False(rows[1].Ticked);
        Assert.Equal(CapReason, rows[1].Reason);
        Assert.Null(rows[1].Page);
    }

    [Fact, Trait("Lane", "unit")]
    public void With_199_owner_pages_a_single_unknown_concept_block_keeps_its_page()
    {
        var row = Preview("Home\nPay -> aproval", ownerPages: 199)[0];

        Assert.False(row.Ticked);
        Assert.Equal(UnknownReason, row.Reason);
        Assert.NotNull(row.Page);
    }
}

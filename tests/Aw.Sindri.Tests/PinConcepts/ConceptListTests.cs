using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Tests.PinConcepts;

/// <summary>The Add several list (concept pins spec §3.6): one concept per line or per block, the name and definition split, bullets dropped, a trailing '(also: …)' as words.</summary>
[Lane(TestLane.Unit)]
public sealed class ConceptListTests
{
    private static ConceptListRead Read(string text)
    {
        var read = ConceptList.Parse(text);
        Assert.Null(read.NamelessLine);
        return read;
    }

    [Fact]
    public void A_line_splits_at_the_first_colon()
    {
        var read = Read("Billing: How we take money: cards and invoices");

        var concept = Assert.Single(read.Concepts);
        Assert.Equal(("Billing", "How we take money: cards and invoices"), (concept.Name, concept.Summary));
    }

    [Theory]
    [InlineData("Billing - How we take money")]
    [InlineData("Billing – How we take money")]
    [InlineData("Billing. How we take money")]
    public void A_line_splits_at_a_spaced_dash_an_en_dash_or_the_end_of_the_first_sentence(string line)
    {
        var concept = Assert.Single(Read(line).Concepts);

        Assert.Equal(("Billing", "How we take money"), (concept.Name, concept.Summary));
    }

    [Fact]
    public void A_line_with_no_delimiter_is_a_name_with_no_definition()
    {
        var concept = Assert.Single(Read("Billing").Concepts);

        Assert.Equal("Billing", concept.Name);
        Assert.Null(concept.Summary);
    }

    [Fact]
    public void Without_a_blank_line_every_line_is_its_own_concept()
    {
        var read = Read("Billing: money\nAuth: who you are\nShifts");

        Assert.Equal("Billing|Auth|Shifts", string.Join('|', read.Concepts.Select(static c => c.Name)));
    }

    [Fact]
    public void A_block_is_a_name_line_and_its_definition_lines_and_blocks_are_split_by_blank_lines()
    {
        var read = Read("Billing\nHow we take money\nfrom customers\n\nAuth: who you are");

        Assert.Equal("Billing=How we take money from customers|Auth=who you are", string.Join('|', read.Concepts.Select(static c => $"{c.Name}={c.Summary}")));
    }

    [Fact]
    public void Leading_bullets_and_numbers_are_dropped()
    {
        var read = Read("- Billing: money\n* Auth: login\n1. Shifts: rota\n2) Orders: carts");

        Assert.Equal("Billing|Auth|Shifts|Orders", string.Join('|', read.Concepts.Select(static c => c.Name)));
    }

    [Fact]
    public void A_trailing_also_gives_the_aliases_and_leaves_the_definition_without_it()
    {
        var concept = Assert.Single(Read("Billing: How we take money (also: payments, invoicing)").Concepts);

        Assert.Equal("How we take money", concept.Summary);
        Assert.Equal("payments|invoicing", string.Join('|', concept.Aliases));
    }

    [Fact]
    public void An_also_on_the_last_line_of_a_block_gives_the_aliases()
    {
        var concept = Read("Billing\nHow we take money (also: payments)\n\nAuth: who you are").Concepts[0];

        Assert.Equal(("Billing", "How we take money"), (concept.Name, concept.Summary));
        Assert.Equal("payments", string.Join('|', concept.Aliases));
    }

    [Fact]
    public void A_line_with_no_name_names_its_number_and_nothing_is_read()
    {
        var read = ConceptList.Parse("Billing: money\nAuth: login\nShifts: rota\n: nothing before the colon");

        Assert.Equal(4, read.NamelessLine);
        Assert.Empty(read.Concepts);
    }

    [Fact]
    public void More_than_a_hundred_concepts_take_the_first_hundred_and_count_the_rest()
    {
        var read = Read(string.Join('\n', Enumerable.Range(1, 103).Select(static n => "Concept" + n.ToString(CultureInfo.InvariantCulture) + ": definition")));

        Assert.Equal((100, 3, "Concept100"), (read.Concepts.Count, read.Skipped, read.Concepts[^1].Name));
    }
}

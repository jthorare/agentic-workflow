using Aw.Sindri.Ui.ViewModels.Context.Concepts;

namespace Aw.Sindri.Ui.Tests.Context.Concepts;

/// <summary>The Add several list parser (concept pins spec §3.6, phase Q7b): a pure text-to-concepts read.</summary>
public sealed class PinListParserTests
{
    private static PinListLine One(string text) => Assert.Single(PinListParser.Read(text).Lines);

    [Theory, Trait("Lane", "unit")]
    [InlineData("Payments: How we take money", "Payments", "How we take money")]
    [InlineData("Payments - How we take money", "Payments", "How we take money")]
    [InlineData("Payments – How we take money", "Payments", "How we take money")]
    [InlineData("Payments. How we take money. Fast.", "Payments", "How we take money. Fast.")]
    [InlineData("Payments.", "Payments", null)]
    [InlineData("Payments", "Payments", null)]
    [InlineData("Self-scheduling: people book slots", "Self-scheduling", "people book slots")]
    [InlineData("Billing: see https://x.test/a", "Billing", "see https://x.test/a")]
    public void A_single_line_splits_at_the_first_colon_dash_or_end_of_sentence(string line, string name, string? definition)
    {
        var read = One(line);

        Assert.Equal(name, read.Name);
        Assert.Equal(definition, read.Definition);
        Assert.True(read.Readable);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_earliest_separator_wins()
    {
        var read = One("Refunds - money back: in full");

        Assert.Equal("Refunds", read.Name);
        Assert.Equal("money back: in full", read.Definition);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("- Payments: money", "Payments")]
    [InlineData("* Payments: money", "Payments")]
    [InlineData("• Payments: money", "Payments")]
    [InlineData("1. Payments: money", "Payments")]
    [InlineData("12) Payments: money", "Payments")]
    [InlineData("  -   Payments: money", "Payments")]
    public void Leading_bullets_and_numbers_are_dropped(string line, string name) => Assert.Equal(name, One(line).Name);

    [Fact, Trait("Lane", "unit")]
    public void A_dash_without_a_space_after_it_is_part_of_the_name() => Assert.Equal("-Payments", One("-Payments: money").Name);

    [Fact, Trait("Lane", "unit")]
    public void A_trailing_also_group_gives_the_words()
    {
        var read = One("Payments: How we take money (also: invoicing,  ledger , Invoicing, login)");

        Assert.Equal("Payments", read.Name);
        Assert.Equal("How we take money", read.Definition);
        Assert.Equal(["invoicing", "ledger", "login"], read.Words);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_also_group_after_a_bare_name_leaves_no_definition()
    {
        var read = One("Payments (also: invoicing)");

        Assert.Equal("Payments", read.Name);
        Assert.Null(read.Definition);
        Assert.Equal(["invoicing"], read.Words);
    }

    [Fact, Trait("Lane", "unit")]
    public void Each_line_is_a_concept_when_the_list_has_no_blank_line()
    {
        var read = PinListParser.Read("Billing\nRefunds\r\n  Sync: calendars\n");

        Assert.Equal(["Billing", "Refunds", "Sync"], read.Lines.Select(l => l.Name), StringComparer.Ordinal);
        Assert.Equal([1, 2, 3], read.Lines.Select(l => l.Number));
        Assert.Equal("calendars", read.Lines[2].Definition);
    }

    [Fact, Trait("Lane", "unit")]
    public void With_blank_lines_a_block_is_a_concept_named_by_its_first_line_and_defined_by_the_rest()
    {
        var read = PinListParser.Read("Refunds\nPutting money back,\nin full or in part\n\nBilling\nPlans and invoices");

        Assert.Equal(["Refunds", "Billing"], read.Lines.Select(l => l.Name), StringComparer.Ordinal);
        Assert.Equal("Putting money back, in full or in part", read.Lines[0].Definition);
        Assert.Equal("Plans and invoices", read.Lines[1].Definition);
    }

    [Fact, Trait("Lane", "unit")]
    public void In_a_block_a_line_with_its_own_separator_starts_the_next_concept()
    {
        var read = PinListParser.Read("Billing: plans\n\nNotifications: emails\nPayments: money (also: ledger)\n\nRefunds\nPutting money back\nPayments: cards\n: nothing here");

        Assert.Equal(["Billing", "Notifications", "Payments", "Refunds", "Payments", string.Empty], read.Lines.Select(l => l.Name), StringComparer.Ordinal);
        Assert.Equal("Putting money back", read.Lines[3].Definition);
        Assert.Equal(["ledger"], read.Lines[2].Words);
        Assert.False(read.Lines[5].Readable);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_also_group_ends_the_whole_block()
    {
        var read = PinListParser.Read("Payments\nHow we take money (also: ledger)\n\nBilling");

        Assert.Equal("How we take money", read.Lines[0].Definition);
        Assert.Equal(["ledger"], read.Lines[0].Words);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_line_with_no_name_or_a_name_over_80_cannot_be_read_and_keeps_its_text()
    {
        var read = PinListParser.Read($": nothing here\n{new string('x', 81)}: long\n{new string('y', 80)}");

        Assert.False(read.Lines[0].Readable);
        Assert.Equal(": nothing here", read.Lines[0].Text);
        Assert.False(read.Lines[1].Readable);
        Assert.True(read.Lines[2].Readable);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_bullet_alone_or_only_an_also_group_has_no_name()
    {
        var read = PinListParser.Read("-  \n(also: a)\n*");

        Assert.All(read.Lines, l => Assert.False(l.Readable));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_blank_or_missing_box_has_no_lines()
    {
        Assert.Empty(PinListParser.Read(text: null).Lines);
        Assert.Empty(PinListParser.Read(" \n\n \t\n").Lines);
    }

    [Fact, Trait("Lane", "unit")]
    public void Only_the_first_100_concepts_are_read()
    {
        var text = string.Join('\n', Enumerable.Range(1, 101).Select(i => "Concept" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        var read = PinListParser.Read(text);

        Assert.Equal(100, read.Lines.Count);
        Assert.True(read.Truncated);
        Assert.Equal("Concept100", read.Lines[^1].Name);
        Assert.False(PinListParser.Read(string.Join('\n', Enumerable.Range(1, 100).Select(i => "C" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)))).Truncated);
    }
}

namespace Aw.Sindri.Ui.Tests.Context.Concepts;

/// <summary>Add several's Esc (concept pins spec §3.6 "Keyboard"): losing a pasted list on a stray Esc asks first.</summary>
public sealed class PinSeveralEscTests
{
    [Fact, Trait("Lane", "unit")]
    public void Esc_on_an_empty_box_closes_without_asking()
    {
        var closed = 0;
        var sheet = new PinSeveralFixtures().Sheet(() => closed++);

        Assert.True(sheet.Escape());

        Assert.Equal(1, closed);
        Assert.False(sheet.IsConfirmingDiscard);
    }

    [Fact, Trait("Lane", "unit")]
    public void Esc_on_a_typed_box_asks_and_a_second_Esc_keeps_editing()
    {
        var closed = 0;
        var sheet = new PinSeveralFixtures().Sheet(() => closed++);
        sheet.Text = "Payments";

        _ = sheet.Escape();
        Assert.True(sheet.IsConfirmingDiscard);
        Assert.Equal("Discard your changes?", sheet.DiscardQuestion);
        _ = sheet.Escape();

        Assert.False(sheet.IsConfirmingDiscard);
        Assert.Equal(0, closed);
        Assert.Equal("Payments", sheet.Text);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Discard_closes_without_writing()
    {
        var closed = 0;
        var fixtures = new PinSeveralFixtures();
        var sheet = await fixtures.Read("Payments\nRefunds", () => closed++);

        _ = sheet.Escape();
        sheet.Discard();

        Assert.Equal(1, closed);
        Assert.Empty(fixtures.Api.PinnedConcepts);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_edited_row_counts_as_a_change_even_when_the_box_is_cleared()
    {
        var sheet = await new PinSeveralFixtures().Read("Payments");
        sheet.Text = string.Empty;
        Assert.False(sheet.IsDirty);

        sheet.Rows[0].Definition = "typed";

        Assert.True(sheet.IsDirty);
    }
}

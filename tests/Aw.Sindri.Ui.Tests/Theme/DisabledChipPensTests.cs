using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Views.Workflows;

namespace Aw.Sindri.Ui.Tests.Theme;

/// <summary>#3648: a disabled graph card dims its status chip with it. The chip takes the card's own disabled fill and border, its text stays readable at 3:1 on the card, and every other state leaves the chip its tone. Tokens exist already; none is added.</summary>
[Lane(TestLane.Unit)]
public sealed class DisabledChipPensTests
{
    private static readonly TokenFile _tokens = TokenFile.Load();

    private static string Token(string brushKey) => brushKey["Brush.".Length..];

    [Fact]
    public void A_disabled_tile_dims_its_chip_with_the_cards_own_fill_and_border_and_keeps_the_text_readable()
    {
        var paint = GraphTilePens.Paint(GraphTileStates.Disabled);

        Assert.Equal((paint.Fill, paint.Border), (paint.ChipFill, paint.ChipBorder));
        Assert.NotNull(paint.ChipInk);
        Assert.True(_tokens.Resources.ContainsKey(paint.ChipInk), $"{paint.ChipInk} is not a token");
        var ratio = _tokens.Contrast(Token(paint.ChipInk), Token(paint.Fill));
        Assert.True(ratio >= 3.0, $"the dimmed chip's text is {ratio:F2}:1 on the card, below 3:1");
    }

    [Theory]
    [InlineData(GraphTileStates.None)]
    [InlineData(GraphTileStates.Hover)]
    [InlineData(GraphTileStates.Error)]
    [InlineData(GraphTileStates.Selected | GraphTileStates.Focus)]
    public void Every_other_state_leaves_the_chip_its_tone(GraphTileStates states)
    {
        var paint = GraphTilePens.Paint(states);

        Assert.Equal((null, null, null), (paint.ChipFill, paint.ChipBorder, paint.ChipInk));
    }
}

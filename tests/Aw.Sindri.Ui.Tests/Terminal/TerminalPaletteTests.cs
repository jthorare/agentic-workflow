using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Terminal;

namespace Aw.Sindri.Ui.Tests.Terminal;

/// <summary>The colours the pane draws for a cell's SGR colour (#3383).</summary>
[Lane(TestLane.Unit)]
public sealed class TerminalPaletteTests
{
    [Fact]
    public void The_default_colour_is_left_to_the_theme() =>
        Assert.Null(TerminalPalette.Resolve(TerminalColor.Default));

    [Theory]
    [InlineData(16, 0, 0, 0)]
    [InlineData(21, 0, 0, 255)]
    [InlineData(196, 255, 0, 0)]
    [InlineData(208, 255, 135, 0)]
    [InlineData(231, 255, 255, 255)]
    public void The_256_cube_uses_six_levels_per_channel(int slot, int r, int g, int b)
    {
        var color = TerminalPalette.Resolve(TerminalColor.Indexed(slot));

        Assert.Equal((r, g, b), (color!.Value.R, color.Value.G, color.Value.B));
    }

    [Theory]
    [InlineData(232, 8)]
    [InlineData(244, 128)]
    [InlineData(255, 238)]
    public void The_last_24_slots_are_a_grey_ramp(int slot, int level)
    {
        var color = TerminalPalette.Resolve(TerminalColor.Indexed(slot));

        Assert.Equal((level, level, level), (color!.Value.R, color.Value.G, color.Value.B));
    }

    [Fact]
    public void Truecolour_and_the_ansi_slots_resolve_as_named()
    {
        var rgb = TerminalPalette.Resolve(TerminalColor.FromRgb(10, 20, 30));
        var red = TerminalPalette.Resolve(TerminalColor.Indexed(1));

        Assert.Equal((10, 20, 30), (rgb!.Value.R, rgb.Value.G, rgb.Value.B));
        Assert.Equal((0xCD, 0x31, 0x31), (red!.Value.R, red.Value.G, red.Value.B));
    }
}

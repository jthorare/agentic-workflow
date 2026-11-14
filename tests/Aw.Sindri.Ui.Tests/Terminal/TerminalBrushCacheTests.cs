using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Terminal;

namespace Aw.Sindri.Ui.Tests.Terminal;

/// <summary>The brush cache (#3383): a frame's cells share one brush per colour.</summary>
[Lane(TestLane.Unit)]
public sealed class TerminalBrushCacheTests
{
    [Fact]
    public void The_same_colour_gives_the_same_brush_and_makes_one()
    {
        var cache = new TerminalBrushCache();

        var a = cache.Get(TerminalColor.Indexed(208));
        var b = cache.Get(TerminalColor.Indexed(208));

        Assert.Same(a, b);
        Assert.Equal(1, cache.Created);
    }

    [Fact]
    public void The_default_colour_has_no_brush_and_makes_none()
    {
        var cache = new TerminalBrushCache();

        Assert.Null(cache.Get(TerminalColor.Default));
        Assert.Equal(0, cache.Created);
    }

    [Fact]
    public void Different_colours_make_different_brushes()
    {
        var cache = new TerminalBrushCache();

        var red = cache.Get(TerminalColor.Indexed(1));
        var truecolour = cache.Get(TerminalColor.FromRgb(1, 2, 3));

        Assert.NotSame(red, truecolour);
        Assert.Equal(2, cache.Created);
    }
}

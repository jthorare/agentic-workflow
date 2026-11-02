using Avalonia;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Rendering.Tests.Views;

/// <summary>UX pass C-6: the mini-map's view rectangle is the part of the world on the stage, never more than the world.</summary>
[Lane(TestLane.Unit)]
public sealed class MiniMapViewportTests
{
    private static readonly Rect _world = new(0, 0, 600, 400);

    [Fact]
    public void A_stage_larger_than_the_world_shows_the_whole_world()
    {
        var visible = MiniMapControl.VisiblePart(new Rect(-40, -20, 1900, 900), _world);

        Assert.Equal(_world, visible);
    }

    [Fact]
    public void A_zoomed_in_stage_is_the_part_of_the_world_it_shows()
    {
        var visible = MiniMapControl.VisiblePart(new Rect(100, 50, 200, 100), _world);

        Assert.Equal(new Rect(100, 50, 200, 100), visible);
    }

    [Fact]
    public void A_stage_that_shows_none_of_the_world_has_no_rectangle()
    {
        var visible = MiniMapControl.VisiblePart(new Rect(2000, 2000, 100, 100), _world);

        Assert.Equal(default, visible);
    }
}

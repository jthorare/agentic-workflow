using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The map's pan and zoom surface under the headless platform: drag, wheel, Ctrl-wheel, the clamp, fit, and reduced motion.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ZoomBorderTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ZoomBorderTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    // A stage of 800 x 600 over a 1360 x 872 world; motion off unless a test turns it on.
    private static (Window Window, ZoomBorder Zoom, Border World) Show(bool motion = false)
    {
        var world = new Border { Width = 1360, Height = 872, Background = Brushes.Transparent };
        var zoom = new ZoomBorder { Child = world, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
        zoom.Resources["Motion.Base"] = motion ? TimeSpan.FromMilliseconds(180) : TimeSpan.Zero;
        var window = new Window { Content = zoom, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, zoom, world);
    }

    [Fact]
    public async Task The_world_is_moved_by_a_matrix_of_the_view() => await OnUi(() =>
    {
        var (window, zoom, world) = Show();
        window.MouseDown(new Point(100, 100), MouseButton.Left);
        window.MouseMove(new Point(140, 130), RawInputModifiers.LeftMouseButton);
        window.MouseUp(new Point(140, 130), MouseButton.Left);

        var matrix = world.RenderTransform!.Value;
        Assert.Equal(new Matrix(1, 0, 0, 1, 40, 30), matrix);
        Assert.Equal(new ZoomView(1, 40, 30), zoom.View);
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_drag_on_empty_canvas_pans_and_is_not_a_click() => await OnUi(() =>
    {
        var (window, zoom, _) = Show();
        var clicks = 0;
        zoom.EmptyClicked += (_, _) => clicks++;

        window.MouseDown(new Point(300, 300), MouseButton.Left);
        window.MouseMove(new Point(250, 320), RawInputModifiers.LeftMouseButton);
        window.MouseUp(new Point(250, 320), MouseButton.Left);

        Assert.Equal(new ZoomView(1, -50, 20), zoom.View);
        Assert.Equal(0, clicks);
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_press_and_release_without_moving_is_a_click_on_empty_canvas() => await OnUi(() =>
    {
        var (window, zoom, _) = Show();
        var clicks = 0;
        zoom.EmptyClicked += (_, _) => clicks++;

        window.MouseDown(new Point(300, 300), MouseButton.Left);
        window.MouseUp(new Point(300, 300), MouseButton.Left);

        Assert.Equal(1, clicks);
        Assert.Equal(ZoomView.Identity, zoom.View);
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_press_a_child_handles_does_not_start_a_pan() => await OnUi(() =>
    {
        var (window, zoom, world) = Show();
        world.PointerPressed += (_, e) => e.Handled = true;

        window.MouseDown(new Point(300, 300), MouseButton.Left);
        window.MouseMove(new Point(350, 340), RawInputModifiers.LeftMouseButton);
        window.MouseUp(new Point(350, 340), MouseButton.Left);

        Assert.Equal(ZoomView.Identity, zoom.View);
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_wheel_pans_and_never_zooms() => await OnUi(() =>
    {
        var (window, zoom, _) = Show();

        window.MouseWheel(new Point(300, 300), new Vector(0, -1));
        window.MouseWheel(new Point(300, 300), new Vector(1, 0));

        Assert.Equal(new ZoomView(1, -40, -40), zoom.View);
        window.Close();
        return true;
    });

    [Theory]
    [InlineData(RawInputModifiers.Control)]
    [InlineData(RawInputModifiers.Meta)]
    public async Task Ctrl_or_Cmd_with_the_wheel_zooms_around_the_cursor(RawInputModifiers modifiers) => await OnUi(() =>
    {
        var (window, zoom, _) = Show();
        var cursor = new Point(400, 250);
        var under = zoom.View.ToWorld(cursor);

        window.MouseWheel(cursor, new Vector(0, -1), modifiers);

        Assert.Equal(1 / 1.1, zoom.View.Scale, 6);
        var after = zoom.View.ToWorld(cursor);
        Assert.Equal(under.X, after.X, 4);
        Assert.Equal(under.Y, after.Y, 4);
        window.Close();
        return true;
    });

    [Fact]
    public async Task Zooming_with_the_wheel_stops_at_62_percent_and_at_100() => await OnUi(() =>
    {
        var (window, zoom, _) = Show();
        for (var i = 0; i < 30; i++)
        {
            window.MouseWheel(new Point(400, 300), new Vector(0, -1), RawInputModifiers.Control);
        }

        Assert.Equal(0.62, zoom.View.Scale, 6);

        for (var i = 0; i < 30; i++)
        {
            window.MouseWheel(new Point(400, 300), new Vector(0, 1), RawInputModifiers.Control);
        }

        Assert.Equal(1.0, zoom.View.Scale, 6);
        window.Close();
        return true;
    });

    [Fact]
    public async Task Fit_with_reduced_motion_is_at_once_and_is_the_pure_fit() => await OnUi(() =>
    {
        var (window, zoom, _) = Show(motion: false);

        zoom.Fit();

        Assert.False(zoom.IsMoving);
        Assert.Equal(ZoomView.Fit(zoom.Bounds.Size, zoom.WorldBounds), zoom.View);
        Assert.True(zoom.View.Scale < 1);
        window.Close();
        return true;
    });

    [Fact]
    public async Task Fit_with_motion_is_a_transition_that_settles_on_the_same_view() => await OnUi(() =>
    {
        var (window, zoom, _) = Show(motion: true);
        var target = ZoomView.Fit(zoom.Bounds.Size, zoom.WorldBounds);

        zoom.Fit();

        Assert.True(zoom.IsMoving);
        Assert.Equal(ZoomView.Identity, zoom.View);
        zoom.Settle();
        Assert.False(zoom.IsMoving);
        Assert.Equal(target, zoom.View);
        window.Close();
        return true;
    });

    [Fact]
    public async Task Touching_the_map_during_a_transition_finishes_it_first() => await OnUi(() =>
    {
        var (window, zoom, _) = Show(motion: true);
        var target = ZoomView.Fit(zoom.Bounds.Size, zoom.WorldBounds);
        zoom.Fit();

        window.MouseWheel(new Point(400, 300), new Vector(0, -1));

        Assert.False(zoom.IsMoving);
        Assert.Equal(target.PanBy(0, -40).Scale, zoom.View.Scale, 6);
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_zoom_steps_go_around_the_middle_of_the_stage() => await OnUi(() =>
    {
        var (window, zoom, _) = Show();
        var middle = new Point(zoom.Bounds.Width / 2, zoom.Bounds.Height / 2);
        var under = zoom.View.ToWorld(middle);

        zoom.ZoomOut();

        Assert.Equal(1 / 1.2, zoom.View.Scale, 6);
        Assert.Equal(under.X, zoom.View.ToWorld(middle).X, 4);
        zoom.ZoomIn();
        Assert.Equal(1.0, zoom.View.Scale, 6);
        window.Close();
        return true;
    });

    [Fact]
    public async Task Reveal_pans_a_node_out_from_under_the_inspector() => await OnUi(() =>
    {
        var (window, zoom, _) = Show();

        zoom.Reveal(new Rect(700, 100, 150, 70), rightInset: 300);

        // right edge 850 ends at 800 - 300 - 24 = 476
        Assert.Equal(476 - 850, zoom.View.X, 6);
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_view_is_a_property_that_raises_when_it_changes() => await OnUi(() =>
    {
        var (window, zoom, _) = Show();
        var seen = new List<ZoomView>();
        zoom.PropertyChanged += (_, e) =>
        {
            if (e.Property == ZoomBorder.ViewProperty)
            {
                seen.Add((ZoomView)e.NewValue!);
            }
        };

        zoom.ZoomOut();
        zoom.ZoomOut();

        Assert.Equal(2, seen.Count);
        Assert.Equal(zoom.View, seen[^1]);
        window.Close();
        return true;
    });

    // C1: a world set before the first layout is checked against a stage with no size; the map must not be pushed aside.
    [Fact]
    public async Task A_world_set_before_the_first_layout_leaves_the_map_where_it_was() => await OnUi(() =>
    {
        var world = new Border { Width = 1360, Height = 872 };
        var zoom = new ZoomBorder { Child = world, WorldBounds = new Rect(0, 0, 2000, 1000) };
        zoom.Resources["Motion.Base"] = TimeSpan.Zero;
        var window = new Window { Content = zoom, Width = 800, Height = 600 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(ZoomView.Identity, zoom.View);
        window.Close();
        return true;
    });

    // C2: the pointer taken from the stage mid-drag ends the drag.
    [Fact]
    public async Task Losing_the_pointer_capture_mid_drag_stops_the_pan() => await OnUi(() =>
    {
        var (window, zoom, _) = Show();
        IPointer? pointer = null;
        zoom.AddHandler(InputElement.PointerPressedEvent, (_, e) => pointer = e.Pointer, handledEventsToo: true);

        window.MouseDown(new Point(300, 300), MouseButton.Left);
        window.MouseMove(new Point(320, 300), RawInputModifiers.LeftMouseButton);
        Assert.Equal(20, zoom.View.X, 6);
        pointer!.Capture(control: null);
        window.MouseMove(new Point(400, 300), RawInputModifiers.LeftMouseButton);

        Assert.Equal(20, zoom.View.X, 6);
        window.Close();
        return true;
    });

    // C4: each stage has its own range and can fit any rectangle.
    [Fact]
    public async Task The_zoom_range_belongs_to_the_stage() => await OnUi(() =>
    {
        var (window, zoom, _) = Show();
        zoom.MinZoom = 0.3;
        zoom.MaxZoom = 1.5;

        for (var i = 0; i < 40; i++)
        {
            window.MouseWheel(new Point(400, 300), new Vector(0, 1), RawInputModifiers.Control);
        }

        Assert.Equal(1.5, zoom.View.Scale, 6);
        for (var i = 0; i < 60; i++)
        {
            window.MouseWheel(new Point(400, 300), new Vector(0, -1), RawInputModifiers.Control);
        }

        Assert.Equal(0.3, zoom.View.Scale, 6);
        window.Close();
        return true;
    });

    [Fact]
    public async Task FitBounds_shows_a_rectangle_wherever_it_is() => await OnUi(() =>
    {
        var (window, zoom, _) = Show();
        zoom.MinZoom = 0.3;
        zoom.MaxZoom = 1.5;

        zoom.FitBounds(new Rect(-400, -300, 800, 600));

        Assert.Equal(new ZoomView(1, 400, 300), zoom.View);
        window.Close();
        return true;
    });

    // C7
    [Fact]
    public async Task The_stage_has_an_automation_name() => await OnUi(() =>
    {
        var (window, zoom, _) = Show();

        Assert.False(string.IsNullOrWhiteSpace(Avalonia.Automation.AutomationProperties.GetName(zoom)));
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_zoom_keys_are_one_entry_point_on_the_stage() => await OnUi(() =>
    {
        var (window, zoom, _) = Show();

        Assert.True(zoom.HandleZoomKey(Key.OemMinus));
        Assert.Equal(1 / 1.2, zoom.View.Scale, 6);
        Assert.True(zoom.HandleZoomKey(Key.OemPlus));
        Assert.Equal(1.0, zoom.View.Scale, 6);
        Assert.True(zoom.HandleZoomKey(Key.Subtract));
        Assert.True(zoom.HandleZoomKey(Key.D0));
        Assert.Equal(ZoomView.Fit(zoom.Bounds.Size, zoom.WorldBounds), zoom.View);
        Assert.False(zoom.HandleZoomKey(Key.A));
        window.Close();
        return true;
    });

    // C8
    [Fact]
    public async Task A_stage_removed_mid_transition_ends_on_its_target() => await OnUi(() =>
    {
        var (window, zoom, _) = Show(motion: true);
        var target = ZoomView.Fit(zoom.Bounds.Size, zoom.WorldBounds);
        zoom.Fit();
        Assert.True(zoom.IsMoving);

        window.Content = null;
        Dispatcher.UIThread.RunJobs();

        Assert.False(zoom.IsMoving);
        Assert.Equal(target, zoom.View);
        window.Close();
        return true;
    });

    // C9
    [Fact]
    public async Task A_change_of_theme_variant_redraws_the_dot_grid_with_fresh_brushes() => await OnUi(() =>
    {
        var (window, zoom, _) = Show();
        var before = zoom.Background;

        window.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
        Dispatcher.UIThread.RunJobs();

        Assert.NotSame(before, zoom.Background);
        window.Close();
        return true;
    });
}

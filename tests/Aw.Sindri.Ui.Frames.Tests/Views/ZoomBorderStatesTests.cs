using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>ZoomBorder fit to host the Workflows graph (#3234): opt-in keyboard focus (the ring is the app-wide adorner), the hand cursor and a panning cursor, a 0.5 to 2 range that holds end to end, and the automation name.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ZoomBorderStatesTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ZoomBorderStatesTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static (Window Window, ZoomBorder Zoom) Show(bool focusable = false, double min = 0.62, double max = 1.0, Rect? world = null)
    {
        var zoom = new ZoomBorder
        {
            Child = new Border { Width = 1360, Height = 872, Background = Brushes.Transparent },
            IsKeyboardFocusable = focusable,
            MinZoom = min,
            MaxZoom = max,
        };
        if (world is { } bounds)
        {
            zoom.WorldBounds = bounds;
        }

        zoom.Resources["Motion.Base"] = TimeSpan.Zero;
        var window = new Window { Content = zoom, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, zoom);
    }

    [Fact]
    public async Task Keyboard_focus_is_off_by_default_so_the_context_map_is_unchanged() => await OnUi(() =>
    {
        var (window, zoom) = Show();

        Assert.False(zoom.IsKeyboardFocusable);
        Assert.False(zoom.Focusable);
        Assert.Equal(0, zoom.BorderThickness.Left);
        window.Close();
        return true;
    });

    [Fact]
    public async Task Opting_in_makes_the_stage_focusable_and_opting_out_again_takes_it_back() => await OnUi(() =>
    {
        var (window, zoom) = Show(focusable: true);
        Assert.True(zoom.Focusable);

        zoom.IsKeyboardFocusable = false;

        Assert.False(zoom.Focusable);
        window.Close();
        return true;
    });

    // The one focus ring is the app-wide adorner (FluentOverrides.axaml, 2 to 4 px outside). A 2 px border on the stage drew a second ring and shifted the content by 2 px on focus.
    [Fact]
    public async Task Keyboard_focus_leaves_the_ring_to_the_app_adorner_and_does_not_move_the_content() => await OnUi(() =>
    {
        var (window, zoom) = Show(focusable: true);
        var child = Assert.IsType<Visual>(zoom.Child, exactMatch: false);
        var before = child.TranslatePoint(default, window);

        Assert.True(zoom.Focus(NavigationMethod.Tab));
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        Assert.Equal(0, zoom.BorderThickness.Left);
        Assert.Equal(before, child.TranslatePoint(default, window));
        Assert.NotNull(zoom.FocusAdorner);
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_click_focus_draws_no_ring() => await OnUi(() =>
    {
        var (window, zoom) = Show(focusable: true);

        Assert.True(zoom.Focus(NavigationMethod.Pointer));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0, zoom.BorderThickness.Left);
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_cursor_is_the_hand_at_rest_and_changes_while_panning_then_comes_back() => await OnUi(() =>
    {
        var (window, zoom) = Show();
        var rest = zoom.Cursor;
        Assert.NotNull(rest);

        window.MouseDown(new Point(100, 100), MouseButton.Left);
        window.MouseMove(new Point(140, 130), RawInputModifiers.LeftMouseButton);
        var panning = zoom.Cursor;
        Assert.Contains(":panning", zoom.Classes, StringComparer.Ordinal);
        window.MouseUp(new Point(140, 130), MouseButton.Left);

        Assert.DoesNotContain(":panning", zoom.Classes, StringComparer.Ordinal);
        Assert.NotSame(rest, panning);
        Assert.Same(rest, zoom.Cursor);
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_stage_set_to_half_to_double_clamps_there_with_the_wheel_the_buttons_and_fit() => await OnUi(() =>
    {
        var (window, zoom) = Show(min: 0.5, max: 2, world: new Rect(0, 0, 4000, 3000));
        for (var i = 0; i < 40; i++)
        {
            window.MouseWheel(new Point(400, 300), new Vector(0, -1), RawInputModifiers.Control);
        }

        Assert.Equal(0.5, zoom.View.Scale, 6);

        for (var i = 0; i < 40; i++)
        {
            window.MouseWheel(new Point(400, 300), new Vector(0, 1), RawInputModifiers.Control);
        }

        Assert.Equal(2.0, zoom.View.Scale, 6);

        zoom.Fit();
        Assert.Equal(0.5, zoom.View.Scale, 6);   // 800 x 600 over 4000 x 3000 fits at 0.2, held at the floor
        for (var i = 0; i < 30; i++)
        {
            zoom.ZoomOut();
        }

        Assert.Equal(0.5, zoom.View.Scale, 6);
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_stage_with_the_defaults_stays_at_62_percent_to_100() => await OnUi(() =>
    {
        var (window, zoom) = Show();

        Assert.Equal(0.62, zoom.MinZoom);
        Assert.Equal(1.0, zoom.MaxZoom);
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_automation_name_is_the_one_the_page_sets() => await OnUi(() =>
    {
        var (window, zoom) = Show();
        AutomationProperties.SetName(zoom, "Workflow canvas");

        Assert.Equal("Workflow canvas", ControlAutomationPeer.CreatePeerForElement(zoom).GetName());
        window.Close();
        return true;
    });
}

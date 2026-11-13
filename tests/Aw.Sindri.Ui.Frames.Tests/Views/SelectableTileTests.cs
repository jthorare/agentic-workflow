using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The selectable tile and row (#3234): rest, hover, pressed, focus, selected and disabled; the row variant fills the width; a click and Enter raise Click.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class SelectableTileTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(SelectableTileTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static (Window Window, SelectableTile Tile, Border Chrome) Show(bool row = false)
    {
        var tile = new SelectableTile { Content = "Run a command" };
        if (row)
        {
            tile.Classes.Add("row");
        }

        var window = new Window { Content = new StackPanel { Margin = new Thickness(20), Children = { tile } }, Width = 400, Height = 200 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return (window, tile, tile.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_Chrome"));
    }

    private static Color Brush(string key) => Application.Current!.FindResource(key) is ISolidColorBrush b ? b.Color : default;

    private static Color Fill(Border chrome) => chrome.Background is ISolidColorBrush b ? b.Color : default;

    private static Color Edge(Border chrome) => chrome.BorderBrush is ISolidColorBrush b ? b.Color : default;

    private static Point Centre(Visual v, Visual root) => v.TranslatePoint(new Point(v.Bounds.Width / 2, v.Bounds.Height / 2), root) ?? default;

    [Fact]
    public async Task At_rest_the_tile_is_transparent_with_a_hairline_and_at_least_28_px_tall() => await OnUi(() =>
    {
        var (window, tile, chrome) = Show();

        Assert.Equal(Colors.Transparent, Fill(chrome));
        Assert.Equal(Brush("Brush.Hairline"), Edge(chrome));
        Assert.True(tile.Bounds.Height >= 28, tile.Bounds.Height.ToString(CultureInfo.InvariantCulture));
        window.Close();
        return true;
    });

    [Fact]
    public async Task Hover_fills_Surface2_and_pressed_fills_Surface3() => await OnUi(() =>
    {
        var (window, tile, chrome) = Show();
        var point = Centre(tile, window);

        window.MouseMove(point, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(Brush("Brush.Surface2"), Fill(chrome));

        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(Brush("Brush.Surface3"), Fill(chrome));
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        window.Close();
        return true;
    });

    [Fact]
    public async Task Keyboard_focus_draws_a_2_px_PrimaryHover_ring() => await OnUi(() =>
    {
        var (window, tile, chrome) = Show();

        Assert.True(tile.Focus(NavigationMethod.Tab));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Brush("Brush.PrimaryHover"), Edge(chrome));
        Assert.Equal(2, chrome.BorderThickness.Left);
        window.Close();
        return true;
    });

    [Fact]
    public async Task Selected_is_a_PrimarySoft_fill_with_a_Primary_border_and_survives_hover() => await OnUi(() =>
    {
        var (window, tile, chrome) = Show();
        tile.IsSelected = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Brush("Brush.PrimarySoft"), Fill(chrome));
        Assert.Equal(Brush("Brush.Primary"), Edge(chrome));
        window.MouseMove(Centre(tile, window), RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(Brush("Brush.PrimarySoft"), Fill(chrome));
        window.Close();
        return true;
    });

    [Fact]
    public async Task Disabled_dims_the_word_and_ignores_hover_and_clicks() => await OnUi(() =>
    {
        var (window, tile, chrome) = Show();
        var clicks = 0;
        tile.Click += (_, _) => clicks++;
        tile.IsEnabled = false;
        Dispatcher.UIThread.RunJobs();
        var point = Centre(tile, window);

        window.MouseMove(point, RawInputModifiers.None);
        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Colors.Transparent, Fill(chrome));
        Assert.Equal(0, clicks);
        Assert.Equal(Brush("Brush.InkSubtle"), Assert.IsType<ISolidColorBrush>(tile.GetVisualDescendants().OfType<TextBlock>().Single().Foreground, exactMatch: false).Color);
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_click_and_Enter_both_raise_Click() => await OnUi(() =>
    {
        var (window, tile, _) = Show();
        var clicks = 0;
        tile.Click += (_, _) => clicks++;
        var point = Centre(tile, window);

        window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Assert.True(tile.Focus(NavigationMethod.Tab));
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, clicks);
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_row_variant_fills_the_width_and_left_aligns_its_content_the_tile_hugs_it() => await OnUi(() =>
    {
        var (window, tile, _) = Show();
        var (rowWindow, row, _) = Show(row: true);

        Assert.True(tile.Bounds.Width < 200, "tile is " + tile.Bounds.Width.ToString(CultureInfo.InvariantCulture));
        Assert.Equal(360, row.Bounds.Width, 1);
        Assert.Equal(Avalonia.Layout.HorizontalAlignment.Left, row.HorizontalContentAlignment);
        window.Close();
        rowWindow.Close();
        return true;
    });

    [Fact]
    public async Task A_screen_reader_hears_selected_and_a_row_is_a_list_item() => await OnUi(() =>
    {
        var (window, tile, _) = Show(row: true);
        var (plainWindow, plain, _) = Show();
        var peer = ControlAutomationPeer.CreatePeerForElement(tile);
        var item = Assert.IsType<ISelectionItemProvider>(peer, exactMatch: false);

        Assert.False(item.IsSelected);
        var selectedPeer = Assert.IsType<SelectableTileAutomationPeer>(peer);
        tile.IsSelected = true;
        Assert.True(item.IsSelected);
        Assert.Equal(1, selectedPeer.SelectedChangesRaised);
        tile.IsSelected = false;
        Assert.Equal(2, selectedPeer.SelectedChangesRaised);
        tile.IsSelected = true;
        Assert.Equal(AutomationControlType.ListItem, peer.GetAutomationControlType());
        Assert.Equal(AutomationControlType.Button, ControlAutomationPeer.CreatePeerForElement(plain).GetAutomationControlType());

        var clicks = 0;
        tile.Click += (_, _) => clicks++;
        item.Select();
        Assert.Equal(1, clicks);
        window.Close();
        plainWindow.Close();
        return true;
    });
}

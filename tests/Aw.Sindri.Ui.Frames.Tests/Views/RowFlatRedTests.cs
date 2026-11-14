using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Theme;
using Aw.Sindri.Ui.ViewModels.Rows;
using Aw.Sindri.Ui.Views.Controls.Rows;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// The flat Row's contract (#3189, flat-row-plan.md sections 2 to 9), red until the slices land: an idle row has no child controls, a hot row has only its action strip, the row
/// peer is a ListItem that reports selection and place, the spoken name keeps its order, the driver finds a row by the text it draws, and the text the row drew never goes stale
/// after a theme, text scale, scaling or text change. The peer-type pin states today's peer so the change shows in the diff.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class RowFlatRedTests
{
    private const string Spoken = "in review, Row 1, In review, 3/8, +120 -4, acme/exports, Maya, waits on you, 12m";

    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(RowFlatRedTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    private static (Window Window, RowList List) Show(IEnumerable<IRowGroupHeading> groups)
    {
        var list = new RowList { ItemsSource = groups, Columns = RowGallery.Columns };
        var window = new Window { Content = list, Width = 1280, Height = 600 };
        window.Show();
        Settle(window);
        return (window, list);
    }

    private static List<Row> Rows(RowList list) =>
        [.. list.GetVisualDescendants().OfType<Row>().Where(r => r.IsEffectivelyVisible && r.Bounds.Height > 0).OrderBy(r => r.TranslatePoint(default, list)?.Y ?? 0)];

    private static Color Ink(Window window, string key) =>
        window.TryFindResource(key, window.ActualThemeVariant, out var found) && found is ISolidColorBrush brush ? brush.Color : default;

    private static Color Of(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : default;

    private static (Window Window, RowList List, CountingCommand Command, Row Row) ShowWithAction()
    {
        var command = new CountingCommand();
        var (window, list) = Show([RowGallery.Group("Needs you", 4, 0, i => RowGallery.Row(i, action: command))]);
        return (window, list, command, Rows(list)[1]);
    }

    [Fact]
    public async Task An_idle_row_has_no_child_controls() => await OnUi(() =>
    {
        var (window, _, _, row) = ShowWithAction();

        Assert.Empty(row.GetVisualChildren());
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_focused_row_gets_its_actions_and_invoking_one_through_its_peer_runs_the_command() => await OnUi(() =>
    {
        var (window, _, command, row) = ShowWithAction();
        Assert.Empty(row.GetVisualDescendants().OfType<Button>());

        Assert.True(row.Focus(NavigationMethod.Directional));
        Settle(window);
        var button = Assert.Single(row.GetVisualDescendants().OfType<Button>());
        var invoke = ControlAutomationPeer.CreatePeerForElement(button).GetProvider<IInvokeProvider>();
        Assert.NotNull(invoke);
        invoke.Invoke();

        Assert.Equal(1, command.Runs);
        _ = Assert.Single(ControlAutomationPeer.CreatePeerForElement(row).GetChildren());   // the peer's children are the strip's buttons while it is attached
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_row_peer_is_a_ListItem_that_is_a_control_element() => await OnUi(() =>
    {
        var (window, _, _, row) = ShowWithAction();

        var peer = ControlAutomationPeer.CreatePeerForElement(row);

        Assert.Equal(("RowAutomationPeer", AutomationControlType.ListItem, true), (peer.GetType().Name, peer.GetAutomationControlType(), peer.IsControlElement()));
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_row_is_a_ListItem_that_reports_its_selection_and_its_place_in_the_list() => await OnUi(() =>
    {
        var group = RowGallery.Group("Needs you", 5);
        var (window, list) = Show([group]);
        list.SelectedItem = group.Rows[2];
        Settle(window);

        var peer = ControlAutomationPeer.CreatePeerForElement(Rows(list)[2]);
        var item = peer.GetProvider<ISelectionItemProvider>();
        Assert.NotNull(item);
        Assert.Equal(AutomationControlType.ListItem, peer.GetAutomationControlType());
        Assert.True(item.IsSelected);
        Assert.Equal(3, AutomationProperties.GetPositionInSet(Rows(list)[2]));
        Assert.Equal(5, AutomationProperties.GetSizeOfSet(Rows(list)[2]));
        var selection = ControlAutomationPeer.CreatePeerForElement(list).GetProvider<ISelectionProvider>();
        Assert.NotNull(selection);
        _ = Assert.Single(selection.GetSelection());
        window.Close();
        return true;
    });

    [Fact]
    public async Task The_spoken_name_keeps_the_order_state_title_facts_who_and_age() => await OnUi(() =>
    {
        var (window, list) = Show([RowGallery.Group("Needs you", 3)]);

        Assert.Equal(Spoken, AutomationProperties.GetName(Rows(list)[1]));
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_row_marks_itself_as_drawn_text_and_carries_its_spoken_name_for_the_driver() => await OnUi(() =>
    {
        var (window, list) = Show([RowGallery.Group("Needs you", 3)]);
        var row = Rows(list)[1];

        // aw-ui-driver's UiTree reports the Name of a control with this class as its text, so `textContains` finds the row by its title or a fact.
        Assert.Contains("aw-drawn-text", row.Classes, StringComparer.Ordinal);
        Assert.Equal(Spoken, AutomationProperties.GetName(row));
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_theme_flip_redraws_every_row_in_the_new_ink_including_rows_recycled_after_it() => await OnUi(() =>
    {
        var (window, list) = Show(RowGallery.Many(1, 200));

        foreach (var variant in new[] { ThemeVariant.Dark, ThemeVariant.Light, ThemeVariant.Dark })
        {
            window.RequestedThemeVariant = variant;
            Settle(window);
            list.ScrollTo(3200);
            Settle(window);
            Assert.Equal(Ink(window, "Brush.InkSubtle"), Of(Rows(list)[0].Layout.IdInk));
            Assert.Equal(Ink(window, "Brush.InkMuted"), Of(Rows(list)[0].Layout.TitleInk));
            list.ScrollTo(0);
            Settle(window);
        }

        window.Close();
        return true;
    });

    [Fact]
    public async Task A_text_scale_change_relays_the_text_at_the_new_size_and_back() => await OnUi(() =>
    {
        var resources = Application.Current!.Resources;
        var (window, list) = Show(RowGallery.Many(1, 20));
        try
        {
            TextScale.Apply(resources, 1.3);
            Settle(window);
            Assert.Equal(Caption(window), Rows(list)[0].Layout.CaptionSize, 0.01);
        }
        finally
        {
            TextScale.Apply(resources, 1.0);
        }

        Settle(window);
        Assert.Equal(Caption(window), Rows(list)[0].Layout.CaptionSize, 0.01);
        window.Close();
        return true;
    });

    private static double Caption(Window window) =>
        window.TryFindResource("Type.Caption.Size", window.ActualThemeVariant, out var found) && found is double size ? size : 0;

    [Fact]
    public async Task A_row_detached_during_a_theme_flip_draws_the_new_ink_when_it_is_attached_again() => await OnUi(() =>
    {
        var list = new RowList { ItemsSource = RowGallery.Many(1, 20), Columns = RowGallery.Columns };
        var window = new Window { Content = list, Width = 1280, Height = 600 };
        var red = Colors.Red;
        window.Resources.ThemeDictionaries[ThemeVariant.Light] = new ResourceDictionary { ["Brush.InkSubtle"] = new SolidColorBrush(red) };
        window.Show();
        Settle(window);
        var row = Rows(list)[0];
        Assert.NotEqual(red, Of(row.Layout.IdInk));

        window.Content = null;   // the list and its rows leave the tree
        window.RequestedThemeVariant = ThemeVariant.Light;
        Settle(window);
        window.Content = list;
        Settle(window);

        Assert.Equal(red, Of(Rows(list)[0].Layout.IdInk));
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_row_listens_for_render_scaling_while_attached_and_stops_when_detached() => await OnUi(() =>
    {
        var (window, list) = Show(RowGallery.Many(1, 20));
        var row = Rows(list)[0];

        Assert.Same(TopLevel.GetTopLevel(row), row.ScalingSource);
        Assert.NotNull(row.ScalingSource);

        window.Content = null;
        Settle(window);
        Assert.Null(row.ScalingSource);
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_render_scaling_change_rebuilds_the_rows_text_layouts() => await OnUi(() =>
    {
        var (window, list) = Show(RowGallery.Many(1, 20));
        var row = Rows(list)[0];
        var before = row.Layout.LayoutsBuilt;

        row.ScalingChangedForTest();
        Settle(window);

        Assert.True(row.Layout.LayoutsBuilt > before, string.Create(CultureInfo.InvariantCulture, $"{row.Layout.LayoutsBuilt} layouts after the change, {before} before"));
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_changed_item_text_redraws_the_row_and_its_name_and_the_driver_text() => await OnUi(() =>
    {
        var group = new LiveGroup("Needs you", Enumerable.Range(0, 4).Select(i => LiveRow.At(i)));
        var (window, list) = Show([group]);
        var row = Rows(list).First(r => r.DataContext is IRowItem item && string.Equals(item.Key, "k1", StringComparison.Ordinal));

        group.Rows[1].Title = "Failed: could not send";
        group.Rows[1].Age = "9m";
        group.Rows[1].Facts = new Dictionary<string, RowFact>(StringComparer.Ordinal) { ["ref"] = new(RowFactKind.Ref, "acme/other") };
        Settle(window);

        Assert.Equal(("Failed: could not send", "9m"), (row.Layout.Title, row.Layout.Age));
        Assert.Contains("acme/other", row.Layout.Facts, StringComparer.Ordinal);
        Assert.Contains("Failed: could not send", AutomationProperties.GetName(row), StringComparison.Ordinal);
        Assert.Contains("aw-drawn-text", row.Classes, StringComparer.Ordinal);
        window.Close();
        return true;
    });

    [Fact]
    public async Task A_chip_is_drawn_in_the_fill_and_ink_of_the_shared_status_chip_for_its_tone() => await OnUi(() =>
    {
        var group = new LiveGroup("Needs you", Enumerable.Range(0, 3).Select(i => LiveRow.At(i)));
        group.Rows[0].Facts = new Dictionary<string, RowFact>(StringComparer.Ordinal) { ["status"] = new(RowFactKind.Chip, "CI red", Tone: RowTone.Danger) };
        group.Rows[1].Facts = new Dictionary<string, RowFact>(StringComparer.Ordinal) { ["status"] = new(RowFactKind.Chip, "2 of 2 ok", Tone: RowTone.Success) };
        group.Rows[2].Facts = new Dictionary<string, RowFact>(StringComparer.Ordinal) { ["status"] = new(RowFactKind.Chip, "No tone") };
        var (window, list) = Show([group]);

        foreach (var (index, tone) in new[] { (0, "Error"), (1, "Ok"), (2, "Neutral") })
        {
            var chip = Assert.Single(Rows(list)[index].Layout.Chips);
            Assert.Equal(Ink(window, $"Brush.Status.{tone}.Bg"), Of(chip.Fill));
            Assert.Equal(Ink(window, $"Brush.Status.{tone}.Fg"), Of(chip.Ink));
        }

        window.Close();
        return true;
    });

    [Fact]
    public async Task A_window_at_render_scaling_2_from_the_first_frame_lays_out_every_fact_text() => await OnUi(() =>
    {
        var list = new RowList { ItemsSource = new[] { RowGallery.Group("Needs you", 4) }, Columns = RowGallery.Columns };
        var window = new Window { Content = list, Width = 1440, Height = 600 };
        Assert.Null(SetHostScaling(window, 2));   // Avalonia.Headless raises it by reflection: IHeadlessWindow is internal
        window.Show();
        Settle(window);

        var layout = Rows(list)[1].Layout;

        Assert.Equal(2, window.RenderScaling);
        Assert.Equal(RowGallery.Columns.Count, layout.Cells.Count);
        Assert.All(layout.Cells, cell => Assert.True(cell.Text.Length > 0 && cell.TextWidth > 0, $"the {cell.Key} fact has no drawn text at render scaling 2"));
        window.Close();
        return true;
    });

    // Null when the window now has this render scaling; otherwise why it could not be set.
    internal static string? SetHostScaling(Window window, double scaling)
    {
        var type = typeof(AvaloniaHeadlessPlatform).Assembly.GetType("Avalonia.Headless.IHeadlessWindow", throwOnError: false);
        var method = type?.GetMethod("SetRenderScaling");
        if (type is null || method is null)
        {
            return "IHeadlessWindow.SetRenderScaling not found";
        }

        if (window.PlatformImpl is not { } impl || !type.IsInstanceOfType(impl))
        {
            return "the window's platform impl is not an IHeadlessWindow";
        }

        var parameters = method.GetParameters();
        if (parameters.Length != 1)
        {
            return "SetRenderScaling takes another number of parameters than one";
        }

        _ = method.Invoke(impl, [Convert.ChangeType(scaling, parameters[0].ParameterType, CultureInfo.InvariantCulture)]);
        return null;
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Context.Whole;
using MapView = Aw.Sindri.Ui.Views.Context.Whole.WholeMapView;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>The whole map's "Map items" list from the keyboard (#1076): Enter on a focused row pins that node and opens its card.</summary>
[Lane(TestLane.Integration)]
public sealed class WholeMapItemsKeyboardTests
{
    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public async Task Enter_on_a_focused_map_item_pins_it_and_opens_its_card()
    {
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(WholeMapItemsKeyboardTests).Assembly).Dispatch(() =>
        {
            var map = new WholeMapViewModel(() => TimeZoneInfo.Utc);
            map.Show(WholeMapLayoutTests.Draft("nodes-small"));
            var window = new Window { Content = new MapView { DataContext = map }, Width = 1280, Height = 900 };
            try
            {
                window.Show();
                Settle(window);
                var list = window.GetVisualDescendants().OfType<ListBox>().First(l => string.Equals(l.Name, "WholeItems", StringComparison.Ordinal));
                var first = list.GetVisualDescendants().OfType<ListBoxItem>().First();
                _ = first.Focus(NavigationMethod.Tab);
                Settle(window);

                window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, keySymbol: null);
                Settle(window);
                var chosen = (MapItemViewModel)list.SelectedItem!;
                window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, keySymbol: null);
                Settle(window);

                Assert.Equal(chosen.Key, map.Pinned);
                Assert.True(map.Panel.IsOpen);
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }

            return true;
        }, TestContext.Current.CancellationToken);
    }

    private static void Press(Window window, Key key, PhysicalKey physical)
    {
        window.KeyPress(key, RawInputModifiers.None, physical, keySymbol: null);
        Settle(window);
    }

    private static void ServeEdges(WholeMapViewModel map)
    {
        var keys = map.Geometry!.Nodes.Select(n => n.Key).ToArray();
        map.Panel.Loader = (key, _) =>
        {
            var others = keys.Where(k => !string.Equals(k, key, StringComparison.Ordinal)).Take(2);
            var info = new NodeInfo(key, "feature", key, "test", Confirmed: true, "high", Confidence: 1.0);
            var edges = others.Select(o => new NodeEdge(key, "relates", o, "test", Confirmed: true)).ToArray();
            return Task.FromResult<Result<NodeView>?>(Results.Success(new NodeView("v1", info, Aliases: [], Out: edges, In: [])));
        };
    }

    [Fact]
    public async Task Enter_on_an_edge_row_keeps_focus_in_the_card()
    {
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(WholeMapItemsKeyboardTests).Assembly).Dispatch(async () =>
        {
            var map = new WholeMapViewModel(() => TimeZoneInfo.Utc);
            map.Show(WholeMapLayoutTests.Draft("nodes-small"));
            ServeEdges(map);
            var window = new Window { Content = new MapView { DataContext = map }, Width = 1280, Height = 900 };
            try
            {
                window.Show();
                Settle(window);
                var list = window.GetVisualDescendants().OfType<ListBox>().First(l => string.Equals(l.Name, "WholeItems", StringComparison.Ordinal));
                _ = list.GetVisualDescendants().OfType<ListBoxItem>().First().Focus(NavigationMethod.Tab);
                Settle(window);
                Press(window, Key.Down, PhysicalKey.ArrowDown);
                Press(window, Key.Enter, PhysicalKey.Enter);
                await map.Panel.Reading;
                Settle(window);

                var card = window.GetVisualDescendants().OfType<Border>().First(b => string.Equals(b.Name, "WholeNodePanel", StringComparison.Ordinal));
                var edge = card.GetVisualDescendants().OfType<ItemsControl>().First(i => string.Equals(i.Name, "WholeNodeGroups", StringComparison.Ordinal)).GetVisualDescendants().OfType<Button>().First();
                var before = map.Panel.Key;
                _ = edge.Focus(NavigationMethod.Tab);
                Settle(window);
                Press(window, Key.Enter, PhysicalKey.Enter);
                await map.Panel.Reading;
                Settle(window);

                Assert.NotEqual(before, map.Panel.Key, StringComparer.Ordinal);
                var focused = Assert.IsType<Visual>(window.FocusManager.GetFocusedElement(), exactMatch: false);
                Assert.True(card.IsVisualAncestorOf(focused));
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }

            return true;
        }, TestContext.Current.CancellationToken);
    }
}

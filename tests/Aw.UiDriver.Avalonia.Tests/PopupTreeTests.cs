using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Aw.UiDriver.Core.Protocol;

namespace Aw.UiDriver.Avalonia.Tests;

/// <summary>A popup that draws in its own popup window (not the overlay layer) is outside the main window's visual tree; the driver still finds and clicks what is in it.</summary>
public sealed class PopupTreeTests
{
    private static readonly Selector _takeOver = new(AutomationId: "take-over");

    // The popup's content is a button, opened the way a menu flyout opens: in a popup window, not in the window's overlay layer.
    private static Task<Popup> OpenPopup(TestScreen screen, CountingCommand command) => TestScreen.OnUi(() =>
    {
        var item = new Button { Content = "Take over", Command = command };
        AutomationProperties.SetAutomationId(item, "take-over");
        var popup = new Popup { PlacementTarget = screen.Save, Child = item, ShouldUseOverlayLayer = false };
        if (screen.Window.Content is StackPanel form)
        {
            form.Children.Add(popup);
        }

        popup.IsOpen = true;
        return popup;
    });

    [Fact, Trait("Lane", "integration")]
    public async Task An_open_popups_content_is_found_and_clicked_and_gone_once_it_closes()
    {
        var screen = await TestScreen.OpenAsync();
        var host = new DriverHost(() => screen.Window, TimeProvider.System);
        var command = new CountingCommand();
        var popup = await OpenPopup(screen, command);

        var found = await host.Search(new SearchRequest(_takeOver));
        var clicked = await host.Click(new ClickRequest(_takeOver));
        var tree = (await host.Tree(new TreeRequest())).Value.Root;
        _ = await TestScreen.OnUi(() => popup.IsOpen = false);
        var after = await host.Search(new SearchRequest(_takeOver));

        Assert.Equal(1, found.Value.Total);
        Assert.Equal("Take over", found.Value.Matches[0].Text);
        Assert.True(clicked.IsSuccess, clicked.IsError ? clicked.Error.Message : string.Empty);
        Assert.Equal(1, command.Runs);
        Assert.Contains(Flatten(tree), static n => n.AutomationId == "take-over");
        Assert.Equal(0, after.Value.Total);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_popup_opened_from_another_window_is_not_in_this_windows_tree()
    {
        var screenA = await TestScreen.OpenAsync();
        var screenB = await TestScreen.OpenAsync();
        var hostB = new DriverHost(() => screenB.Window, TimeProvider.System);
        var otherWindowItem = new Selector(AutomationId: "other-window-item");
        var popup = await TestScreen.OnUi(() =>
        {
            var item = new Button { Content = "Other window item" };
            AutomationProperties.SetAutomationId(item, "other-window-item");
            var opened = new Popup { PlacementTarget = screenA.Save, Child = item, ShouldUseOverlayLayer = false };
            if (screenA.Window.Content is StackPanel form)
            {
                form.Children.Add(opened);
            }

            opened.IsOpen = true;
            return opened;
        });

        var found = await hostB.Search(new SearchRequest(otherWindowItem));
        var tree = (await hostB.Tree(new TreeRequest())).Value.Root;
        _ = await TestScreen.OnUi(() => popup.IsOpen = false);

        Assert.Equal(0, found.Value.Total);
        Assert.DoesNotContain(Flatten(tree), static n => n.AutomationId == "other-window-item");
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_menu_flyout_shown_on_a_button_is_in_its_windows_tree()
    {
        var screen = await TestScreen.OpenAsync();
        var host = new DriverHost(() => screen.Window, TimeProvider.System);
        var menuItem = new Selector(AutomationId: "flyout-item");
        var flyout = await TestScreen.OnUi(() =>
        {
            var item = new MenuItem { Header = "Flyout item" };
            AutomationProperties.SetAutomationId(item, "flyout-item");
            var shown = new MenuFlyout { Items = { item } };
            shown.ShowAt(screen.Save);
            return shown;
        });

        var found = await host.Search(new SearchRequest(menuItem));
        _ = await TestScreen.OnUi(() =>
        {
            flyout.Hide();
            return 0;
        });

        Assert.Equal(1, found.Value.Total);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_context_menu_opened_on_a_button_is_in_its_windows_tree()
    {
        var screen = await TestScreen.OpenAsync();
        var host = new DriverHost(() => screen.Window, TimeProvider.System);
        var menuItem = new Selector(AutomationId: "context-item");
        var menu = await TestScreen.OnUi(() =>
        {
            var item = new MenuItem { Header = "Context item" };
            AutomationProperties.SetAutomationId(item, "context-item");
            var opened = new ContextMenu { Items = { item } };
            opened.Open(screen.Save);
            return opened;
        });

        var found = await host.Search(new SearchRequest(menuItem));
        _ = await TestScreen.OnUi(() =>
        {
            menu.Close();
            return 0;
        });

        Assert.Equal(1, found.Value.Total);
    }

    private static IEnumerable<NodeInfo> Flatten(NodeInfo node) => new[] { node }.Concat(node.Children.SelectMany(Flatten));
}

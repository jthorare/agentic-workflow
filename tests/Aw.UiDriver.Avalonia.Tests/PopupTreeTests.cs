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

    private static IEnumerable<NodeInfo> Flatten(NodeInfo node) => new[] { node }.Concat(node.Children.SelectMany(Flatten));
}

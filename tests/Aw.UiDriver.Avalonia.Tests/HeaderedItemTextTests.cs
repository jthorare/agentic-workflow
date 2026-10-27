using Avalonia.Controls;
using Aw.UiDriver.Core.Protocol;

namespace Aw.UiDriver.Avalonia.Tests;

/// <summary>A menu item or tab item shows its header as text, so a selector's text finds it.</summary>
public sealed class HeaderedItemTextTests
{
    [Fact, Trait("Lane", "integration")]
    public async Task A_menu_item_and_a_tab_item_are_found_by_their_header_text()
    {
        var screen = await TestScreen.OpenAsync();
        _ = await TestScreen.OnUi(() =>
        {
            if (screen.Window.Content is StackPanel form)
            {
                form.Children.Add(new MenuItem { Header = "Take over" });
                form.Children.Add(new TabItem { Header = "Work" });
            }

            return true;
        });
        var host = new DriverHost(() => screen.Window, TimeProvider.System);

        var menu = (await host.Search(new SearchRequest(new Selector(Type: "MenuItem", Text: "Take over")))).Value;
        var tab = (await host.Search(new SearchRequest(new Selector(Type: "TabItem", Text: "Work")))).Value;

        Assert.Equal((1, 1), (menu.Total, tab.Total));
    }
}

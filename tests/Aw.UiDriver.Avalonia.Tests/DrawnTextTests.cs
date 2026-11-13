using Avalonia.Controls;
using Aw.UiDriver.Core.Protocol;

namespace Aw.UiDriver.Avalonia.Tests;

/// <summary>A control that draws its own text sets UiDriverText, and a selector's text and textContains find it (#3189).</summary>
public sealed class DrawnTextTests
{
    [Fact, Trait("Lane", "integration")]
    public async Task A_control_with_drawn_text_is_found_by_its_exact_text_and_by_a_part_of_it()
    {
        var screen = await TestScreen.OpenAsync();
        _ = await TestScreen.OnUi(() =>
        {
            var drawn = new Border { Width = 40, Height = 20 };
            UiDriverText.SetText(drawn, "Failed: could not send");
            if (screen.Window.Content is StackPanel form)
            {
                form.Children.Add(drawn);
            }

            return true;
        });
        var host = new DriverHost(() => screen.Window, TimeProvider.System);

        var exact = (await host.Search(new SearchRequest(new Selector(Type: "Border", Text: "Failed: could not send")))).Value;
        var part = (await host.Search(new SearchRequest(new Selector(Type: "Border", TextContains: "could not")))).Value;

        Assert.Equal((1, 1), (exact.Total, part.Total));
    }
}

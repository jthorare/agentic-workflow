using Avalonia.Automation;
using Avalonia.Controls;
using Aw.UiDriver.Core.Protocol;

namespace Aw.UiDriver.Avalonia.Tests;

/// <summary>A control that draws its own text (no TextBlock inside) and has the class aw-drawn-text reports its automation Name as its text, so a selector's text and textContains find it (#3189).</summary>
public sealed class DrawnTextTests
{
    [Fact, Trait("Lane", "integration")]
    public async Task A_control_with_the_class_aw_drawn_text_is_found_by_its_name_as_text_and_one_without_is_not()
    {
        var screen = await TestScreen.OpenAsync();
        _ = await TestScreen.OnUi(() =>
        {
            var drawn = new DrawnLabel();
            drawn.Classes.Add("aw-drawn-text");
            AutomationProperties.SetName(drawn, "in review, Failed: could not send, waits on you, 9m");
            var plain = new DrawnLabel();
            AutomationProperties.SetName(plain, "in review, Failed: could not send, waits on you, 9m");
            if (screen.Window.Content is StackPanel form)
            {
                form.Children.Add(drawn);
                form.Children.Add(plain);
            }

            return true;
        });
        var host = new DriverHost(() => screen.Window, TimeProvider.System);

        var part = (await host.Search(new SearchRequest(new Selector(Type: "DrawnLabel", TextContains: "could not send")))).Value;
        var exact = (await host.Search(new SearchRequest(new Selector(Type: "DrawnLabel", Text: "in review, Failed: could not send, waits on you, 9m")))).Value;

        Assert.Equal((1, 1), (part.Total, exact.Total));
    }

    /// <summary>A control that paints its text itself: it holds no TextBlock.</summary>
    private sealed class DrawnLabel : Control
    {
        protected override global::Avalonia.Size MeasureOverride(global::Avalonia.Size availableSize) => new(40, 20);
    }
}

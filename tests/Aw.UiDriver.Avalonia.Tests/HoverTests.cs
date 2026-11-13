using Aw.UiDriver.Core.Protocol;

namespace Aw.UiDriver.Avalonia.Tests;

/// <summary>#3467: hover is a real pointer move, so a control that draws from IsPointerOver and its pointer handlers follow it, not only styles.</summary>
public sealed class HoverTests
{
    private static readonly Selector _card = new(AutomationId: "card");

    [Fact, Trait("Lane", "integration")]
    public async Task Hover_moves_a_real_pointer_onto_the_element_so_IsPointerOver_and_its_handlers_follow()
    {
        var screen = await HoverScreen.OpenAsync();
        var host = new DriverHost(() => screen.Window, TimeProvider.System);

        var hovered = await host.Hover(new HoverRequest(_card));
        var state = await TestScreen.OnUi(() => (screen.Card.IsPointerOver, screen.Entered));
        var drawn = await LabelText(host);

        Assert.True(hovered.IsSuccess, hovered.IsError ? hovered.Error.Message : string.Empty);
        Assert.Equal((true, 1), state);
        Assert.Equal("hovered", drawn);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Hover_off_moves_the_pointer_out_so_IsPointerOver_clears_and_the_exit_handler_runs()
    {
        var screen = await HoverScreen.OpenAsync();
        var host = new DriverHost(() => screen.Window, TimeProvider.System);

        _ = await host.Hover(new HoverRequest(_card));
        var off = await host.Hover(new HoverRequest(_card, Off: true));
        var state = await TestScreen.OnUi(() => (screen.Card.IsPointerOver, screen.Exited, screen.Card.Classes.Contains(":pointerover")));
        var drawn = await LabelText(host);

        Assert.True(off.IsSuccess, off.IsError ? off.Error.Message : string.Empty);
        Assert.Equal((false, 1, false), state);
        Assert.Equal("idle", drawn);
    }

    private static async Task<string?> LabelText(DriverHost host)
    {
        var tree = (await host.Tree(new TreeRequest())).Value.Root;
        return Flatten(tree).Single(static n => n.AutomationId == "card-label").Text;
    }

    private static IEnumerable<NodeInfo> Flatten(NodeInfo node) => new[] { node }.Concat(node.Children.SelectMany(Flatten));
}

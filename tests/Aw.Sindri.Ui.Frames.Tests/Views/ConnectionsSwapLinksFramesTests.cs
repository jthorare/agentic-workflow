using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Tests.Views;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// The Connections tile's swap links read as links (#4317, seen in #4364's frames): "Use an OAuth app instead" and "Use a token instead" carry the accent-link
/// class, rest in Brush.Link (the colour Connect Linear uses, #4429) and turn Brush.PrimaryHover under the pointer. Before the fix they drew in muted body ink.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ConnectionsSwapLinksFramesTests : FrameHost
{
    private static Color TokenColor(string key) =>
        Application.Current is { } app && app.TryFindResource(key, app.ActualThemeVariant, out var value) && value is ISolidColorBrush brush ? brush.Color : throw new InvalidOperationException($"{key} is not a brush token");

    // The named button inside this tile's view only: every tile has the same element names.
    private static Button In(Window window, object tile, string name) =>
        window.GetVisualDescendants().OfType<Button>().Single(c => string.Equals(c.Name, name, StringComparison.Ordinal) && c.IsEffectivelyVisible && c.GetVisualAncestors().OfType<Control>().Any(a => a.DataContext == tile));

    private static void AssertLink(Window window, Button button)
    {
        Assert.True(button.Classes.Contains("accent-link"), $"{button.Name} has no accent-link class");
        Assert.Equal(TokenColor("Brush.Link"), (button.Foreground as ISolidColorBrush)?.Color);
        ((IPseudoClasses)button.Classes).Set(":pointerover", value: true);
        Settle(window);
        var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().First(p => string.Equals(p.Name, "PART_ContentPresenter", StringComparison.Ordinal));
        Assert.Equal(TokenColor("Brush.PrimaryHover"), (presenter.Foreground as ISolidColorBrush)?.Color);
        ((IPseudoClasses)button.Classes).Set(":pointerover", value: false);
    }

    [Fact]
    public Task Both_swap_links_rest_in_the_link_colour_and_brighten_under_the_pointer() => OnUi(async () =>
    {
        var (window, shell) = await Connected(width: 1440, height: 900);
        shell.GoTo(PageId.Connections);
        await Until(window, () => shell.Connections.Drawn > 0, "the Connections page never drew");
        var tile = shell.Connections.Tiles.Single(t => string.Equals(t.Id, "github", StringComparison.Ordinal));
        await tile.Connect(CancellationToken.None);
        Settle(window);
        FrameFile.Save(window, "connections-swap-link-oauth-1440");
        AssertLink(window, In(window, tile, "ServiceTileUseOAuth"));

        tile.UseOAuth();
        Settle(window);
        FrameFile.Save(window, "connections-swap-link-token-1440");
        AssertLink(window, In(window, tile, "ServiceTileUseToken"));
    });
}

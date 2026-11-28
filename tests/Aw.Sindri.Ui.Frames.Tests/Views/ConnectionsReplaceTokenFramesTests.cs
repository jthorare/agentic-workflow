using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// "Replace token" on the Connections page (#4358): a connected key tile shows it as an accent link, a connected oauth tile does not. Pressing it opens the paste box; a refused
/// token shows its sentence in the box while the tile stays connected. Frames are saved under frames/ as connections-replace-*.png.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ConnectionsReplaceTokenFramesTests : FrameHost
{
    private static Color TokenColor(string key) =>
        Application.Current is { } app && app.TryFindResource(key, app.ActualThemeVariant, out var value) && value is ISolidColorBrush brush ? brush.Color : throw new InvalidOperationException($"{key} is not a brush token");

    // The named control inside this tile's view only (every tile has the same element names), visible or not.
    private static IEnumerable<T> In<T>(Window window, object tile, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Where(c => string.Equals(c.Name, name, StringComparison.Ordinal) && c.IsEffectivelyVisible && c.GetVisualAncestors().OfType<Control>().Any(a => a.DataContext == tile));

    [Fact]
    public Task The_link_shows_on_a_connected_key_tile_and_not_on_an_oauth_tile() => OnUi(async () =>
    {
        _ = Server.SeedConnection(FakeSindriApi.ConnectionRow("github:work", kind: "rest"));
        _ = Server.SeedConnection(FakeSindriApi.ConnectionRow("linear:work"));
        var (window, shell) = await Connected(width: 1440, height: 900);
        shell.GoTo(PageId.Connections);
        await Until(window, () => shell.Connections.Drawn > 0 && shell.Connections.Rows.Count == 2, "the Connections page never read the list");
        var github = shell.Connections.Tiles.Single(t => string.Equals(t.Id, "github", StringComparison.Ordinal));
        var linear = shell.Connections.Tiles.Single(t => string.Equals(t.Id, "linear", StringComparison.Ordinal));

        var link = Assert.Single(In<Button>(window, github, "ServiceTileReplaceToken"));
        Assert.True(link.Classes.Contains("accent-link"), "the link has no accent-link class");
        Assert.Equal(TokenColor("Brush.Link"), (link.Foreground as ISolidColorBrush)?.Color);
        Assert.Empty(In<Button>(window, linear, "ServiceTileReplaceToken"));
        FrameFile.Save(window, "connections-replace-link-1440");
    });

    [Fact]
    public Task Pressing_it_opens_the_paste_box_and_a_refusal_shows_inside_it() => OnUi(async () =>
    {
        _ = Server.SeedConnection(FakeSindriApi.ConnectionRow("github:work", kind: "rest"));
        var (window, shell) = await Connected(width: 1440, height: 900);
        shell.GoTo(PageId.Connections);
        await Until(window, () => shell.Connections.Drawn > 0 && shell.Connections.Rows.Count == 1, "the Connections page never read the list");
        var github = shell.Connections.Tiles.Single(t => string.Equals(t.Id, "github", StringComparison.Ordinal));
        Assert.Empty(In<TextBox>(window, github, "ServiceTileReplaceField"));

        github.StartReplace();
        Settle(window);
        _ = Assert.Single(In<TextBox>(window, github, "ServiceTileReplaceField"));
        Assert.Empty(In<Button>(window, github, "ServiceTileReplaceToken"));
        FrameFile.Save(window, "connections-replace-box-1440");

        Server.NextReplaceKeyError = new Error("SND-CONN-018", ErrorKind.Invalid, "GitHub refused the key");
        github.ReplaceToken.Text = "ghp_new_token_0002";
        await github.Replace(CancellationToken.None);
        Settle(window);
        var error = Assert.Single(In<TextBlock>(window, github, "ServiceTileReplaceError"));
        Assert.Contains("old one is still in use", error.Text, StringComparison.Ordinal);
        Assert.True(github.IsConnected);
        FrameFile.Save(window, "connections-replace-refused-1440");
    });
}

using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>Key frames for the Connections page, its sheet and its add form at 1440 and 900 px wide, against the in-memory API. Saved under frames/ as connections-*.png.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ConnectionsFramesTests : FrameHost
{
    private const string Prefix = "connections-";
    private const string Key = "sk-test-77c1e09ab4";

    private async Task<(Window Window, ShellViewModel Shell)> OnConnections(double width)
    {
        _ = Server.SeedConnection(FakeSindriApi.ConnectionRow("acme:key", "error", "rest", "the provider refused the refresh"));
        _ = Server.SeedConnection(FakeSindriApi.ConnectionRow("linear:work") with { LastRefreshedAt = A.Noon - TimeSpan.FromMinutes(30) });
        _ = Server.SeedConnection(FakeSindriApi.ConnectionRow("notes:mcp", "needs-sign-in", "mcp", "the token expired"));
        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.Connections);
        await Until(window, () => shell.Connections.Drawn > 0 && shell.Connections.Rows.Count == 3, "the Connections page never read the list");
        return (window, shell);
    }

    [Theory]
    [InlineData(1440)]
    [InlineData(900)]
    public Task The_list_with_a_chip_for_each_status(double width) => OnUi(async () =>
    {
        var (window, shell) = await OnConnections(width);

        Assert.Equal(shell.Connections.Head, Named<TextBlock>(window, "ConnectionsHead").Text);
        Assert.True(Visible(window, "ConnectionRows"));
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => string.Equals(t.Text, "! Needs sign-in", StringComparison.Ordinal));
        Assert.Contains(window.GetVisualDescendants().OfType<Button>(), t => string.Equals(t.Name, "ConnSignIn", StringComparison.Ordinal) && t.IsEffectivelyVisible);
        Frame(window, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"list-{width}"), Prefix);
    });

    [Fact]
    public Task The_empty_list_says_so() => OnUi(async () =>
    {
        var (window, shell) = await Connected(width: 1440, height: 900);
        shell.GoTo(PageId.Connections);
        await Until(window, () => shell.Connections.Drawn > 0, "the Connections page never read the list");

        Assert.True(Visible(window, "ConnectionTiles"));
        Assert.False(Visible(window, "ConnectionsEmptyName"));
        Frame(window, "empty", Prefix);
    });

    [Fact]
    public Task Remove_asks_in_the_row() => OnUi(async () =>
    {
        var (window, shell) = await OnConnections(1440);

        shell.Connections.Rows[0].AskRemove();
        Settle(window);

        Assert.True(Visible(window, "ConnConfirm"));
        Frame(window, "confirm", Prefix);
    });

    [Theory]
    [InlineData(1440)]
    [InlineData(900)]
    public Task The_sheet_of_a_connection_that_needs_a_sign_in(double width) => OnUi(async () =>
    {
        var (window, shell) = await OnConnections(width);

        shell.Navigate(new DeepLink(DeepLinkKind.Connection, "notes:mcp"));
        await Until(window, () => shell.ConnectionSheet.Phase == FramePhase.Shown && shell.IsConnectionSheet);

        Assert.True(Visible(window, "ConnectionSheetActions"));
        Frame(window, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"sheet-{width}"), Prefix);
    });

    [Fact]
    public Task The_key_box_is_masked_and_the_secret_is_in_no_text_of_the_frame() => OnUi(async () =>
    {
        var (window, shell) = await OnConnections(1440);
        var form = shell.Connections.Add;
        form.Open();
        form.SelectedPreset = form.Presets.Single(p => string.Equals(p.Id, "rest", StringComparison.Ordinal));
        form.Name = "acme:other";
        form.Key.Text = Key;
        Settle(window);

        var box = Named<TextBox>(window, "AddConnectionKey");
        Assert.Equal('●', box.PasswordChar);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.Contains(Key, StringComparison.Ordinal) == true);
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), c => Avalonia.Automation.AutomationProperties.GetName(c)?.Contains(Key, StringComparison.Ordinal) == true);
        Frame(window, "add-key", Prefix);
    });

    [Fact]
    public Task A_tile_for_a_sign_in_provider_asks_for_a_client_id_and_says_why_Connect_waits() => OnUi(async () =>
    {
        var (window, shell) = await OnConnections(1440);
        var tile = shell.Connections.Tiles.Single(t => string.Equals(t.Id, "linear", StringComparison.Ordinal));
        await tile.Connect(CancellationToken.None);
        Settle(window);

        Assert.True(Visible(window, "ServiceTileClientId"));
        Assert.True(Visible(window, "ServiceTileReason"));
        Frame(window, "tile-asks", Prefix);
    });
    [Fact]
    public Task A_tile_shows_its_chip_one_primary_button_and_the_help_line_and_nothing_clips_at_900() => OnUi(async () =>
    {
        var (window, shell) = await OnConnections(900);
        var tile = shell.Connections.Tiles.Single(t => string.Equals(t.Id, "linear", StringComparison.Ordinal));
        await tile.Connect(CancellationToken.None);
        Settle(window);

        Assert.True(Visible(window, "ServiceTileChip"));
        Assert.True(Visible(window, "ServiceTileHelp"));
        Assert.True(Visible(window, "ServiceTileHelpLink"));
        Assert.Equal(1, window.GetVisualDescendants().OfType<Button>().Count(b => string.Equals(b.Name, "ServiceTilePrimary", StringComparison.Ordinal) && b.IsEffectivelyVisible && b.GetVisualAncestors().OfType<Control>().Any(c => c.DataContext == tile)));
        Assert.All(window.GetVisualDescendants().OfType<Border>().Where(b => string.Equals(b.Name, "ServiceTileFrame", StringComparison.Ordinal)), b => Assert.True(b.TranslatePoint(new Avalonia.Point(b.Bounds.Width, 0), window)!.Value.X <= window.Bounds.Width));
        Frame(window, "tile-help-900", Prefix);
    });
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>Key frames for the Connections page, its sheet and its add form at 1440 and 900 px wide (the tile check at 1000, the window minimum), against the in-memory API. Saved under frames/ as connections-*.png.</summary>
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
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => string.Equals(t.Text, "Needs sign-in", StringComparison.Ordinal));
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

        Assert.True(VisibleIn(window, tile, "ServiceTileClientId"));
        Assert.True(VisibleIn(window, tile, "ServiceTileReason"));
        Frame(window, "tile-asks", Prefix);
    });

    // 1000 px is the narrowest the window goes (Size.WindowMinWidth): at 900 the layout stays 1000 wide and every frame "clips".
    // Spotify, not Linear: OnConnections seeds linear:work as connected, and a connected tile has no primary button (Reconnect is in its menu).
    [Fact]
    public Task A_tile_shows_its_chip_one_primary_button_and_the_help_line_and_nothing_clips_at_1000() => OnUi(async () =>
    {
        var (window, shell) = await OnConnections(1000);
        var tile = shell.Connections.Tiles.Single(t => string.Equals(t.Id, "spotify", StringComparison.Ordinal));
        await tile.Connect(CancellationToken.None);
        Settle(window);

        Assert.True(VisibleIn(window, tile, "ServiceTileChip"), "the chip is hidden");
        Assert.True(VisibleIn(window, tile, "ServiceTileHelp"), "the help line is hidden");
        Assert.True(VisibleIn(window, tile, "ServiceTileHelpLink"), "the help link is hidden");
        Assert.Equal(1, window.GetVisualDescendants().OfType<Button>().Count(b => string.Equals(b.Name, "ServiceTilePrimary", StringComparison.Ordinal) && b.IsEffectivelyVisible && b.GetVisualAncestors().OfType<Control>().Any(c => c.DataContext == tile)));
        Assert.All(window.GetVisualDescendants().OfType<Border>().Where(b => string.Equals(b.Name, "ServiceTileFrame", StringComparison.Ordinal)), b => Assert.True(b.TranslatePoint(new Point(b.Bounds.Width, 0), window)!.Value.X <= window.Bounds.Width, "a tile runs past the window's right edge"));
        Frame(window, "tile-help-1000", Prefix);
    });

    // #2556: Rename opens an inline field on the tile holding the current label; at 1000 px (the window's narrowest) the field, its Save and its error stay inside the tile.
    [Fact]
    public Task A_tile_in_rename_mode_shows_the_field_with_the_label_and_nothing_clips_at_1000() => OnUi(async () =>
    {
        var (window, shell) = await OnConnections(1000);
        var tile = shell.Connections.Tiles.Single(t => string.Equals(t.Id, "linear", StringComparison.Ordinal));
        tile.StartRename();
        tile.RenameText = "home-office";
        Server.ConnectionWriteError = new Kernel.Error("SND-CONN-010", Kernel.ErrorKind.Conflict, "A connection with that name already exists");
        await tile.Rename(CancellationToken.None);
        Settle(window);

        Assert.True(VisibleIn(window, tile, "ServiceTileRenameField"), "the field is hidden");
        Assert.True(VisibleIn(window, tile, "ServiceTileRenameSave"), "Save is hidden");
        Assert.True(VisibleIn(window, tile, "ServiceTileRenameError"), "the refusal is hidden");
        var field = window.GetVisualDescendants().OfType<TextBox>().Single(b => string.Equals(b.Name, "ServiceTileRenameField", StringComparison.Ordinal) && b.IsEffectivelyVisible);
        Assert.Equal("home-office", field.Text);
        var frame = field.GetVisualAncestors().OfType<Border>().First(b => string.Equals(b.Name, "ServiceTileFrame", StringComparison.Ordinal));
        Assert.True(field.TranslatePoint(new Point(field.Bounds.Width, 0), frame)!.Value.X <= frame.Bounds.Width, "the field ends inside the tile");
        Frame(window, "tile-rename-1000", Prefix);
    });

    // #2556 qa: the field takes the label line's place (between the chip and "Last used"); the label shows once and comes back on Cancel.
    [Fact]
    public Task Renaming_puts_the_field_where_the_label_line_was_and_Cancel_brings_the_line_back() => OnUi(async () =>
    {
        var (window, shell) = await OnConnections(1000);
        var tile = shell.Connections.Tiles.Single(t => string.Equals(t.Id, "linear", StringComparison.Ordinal));
        Settle(window);
        Assert.True(VisibleIn(window, tile, "ServiceTileSubtitle"), "the label line is hidden before renaming");

        tile.StartRename();
        Settle(window);

        Assert.False(VisibleIn(window, tile, "ServiceTileSubtitle"), "the label shows twice");
        var field = window.GetVisualDescendants().OfType<TextBox>().Single(b => string.Equals(b.Name, "ServiceTileRenameField", StringComparison.Ordinal) && b.IsEffectivelyVisible);
        var lastUsed = window.GetVisualDescendants().OfType<TextBlock>().Single(b => string.Equals(b.Name, "ServiceTileLastUsed", StringComparison.Ordinal) && b.IsEffectivelyVisible && b.GetVisualAncestors().OfType<Control>().Any(a => a.DataContext == tile));
        Assert.True(field.TranslatePoint(new Point(0, 0), window)!.Value.Y < lastUsed.TranslatePoint(new Point(0, 0), window)!.Value.Y, "the field sits under Last used");
        Assert.Equal(tile.Text.RenameLabel, Avalonia.Automation.AutomationProperties.GetName(field));

        tile.CancelRename();
        Settle(window);

        Assert.True(VisibleIn(window, tile, "ServiceTileSubtitle"), "the label line did not come back");
    });

    // qa (#2537): the custom row has ONE top edge (the panel's), a "Custom" label sits above it, and every tile's title sits at the same y with or without the menu button.
    [Fact]
    public Task The_custom_section_has_a_label_and_one_top_edge_and_the_tile_titles_line_up() => OnUi(async () =>
    {
        var (window, shell) = await OnConnections(1280);

        Assert.True(Visible(window, "ConnectionsCustomLabel"), "no Custom label above the custom rows");
        var first = window.GetVisualDescendants().OfType<Border>().First(b => string.Equals(b.Name, "ConnectionRowFrame", StringComparison.Ordinal));
        Assert.Equal(0, first.BorderThickness.Top);
        var tops = window.GetVisualDescendants().OfType<Border>()
            .Where(b => string.Equals(b.Name, "ServiceTileFrame", StringComparison.Ordinal))
            .Select(b => b.GetVisualDescendants().OfType<TextBlock>().First().TranslatePoint(new Point(0, 0), b)!.Value.Y)
            .Distinct().ToList();
        _ = Assert.Single(tops);
        Assert.Contains(shell.Connections.Tiles, t => !t.IsOffered);   // a tile with the menu button is among them
        Frame(window, "custom-and-titles", Prefix);
    });

    // Every tile has the same element names, so a lookup by name alone finds the first tile's: look inside this tile's view only.
    private static bool VisibleIn(Window window, object tile, string name) =>
        window.GetVisualDescendants().OfType<Control>().Any(c => c.Name == name && c.IsEffectivelyVisible && c.GetVisualAncestors().OfType<Control>().Any(a => a.DataContext == tile));
}
